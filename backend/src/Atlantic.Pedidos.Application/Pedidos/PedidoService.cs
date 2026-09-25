using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Pedidos;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Atlantic.Pedidos.Application.Pedidos;

public interface IPedidoService
{
    Task<PagedResult<PedidoResumenDto>> ListarAsync(PedidoFiltro filtro, CancellationToken ct);
    Task<PedidoDto> ObtenerAsync(int id, CancellationToken ct);
    Task<SiguienteNumeroDto> SiguienteNumeroAsync(CancellationToken ct);
    Task<PedidoResumenEstadosDto> ResumenAsync(CancellationToken ct);
    Task<PedidoDto> CrearAsync(CrearPedidoRequest request, CancellationToken ct);
    Task<PedidoDto> ActualizarAsync(int id, ActualizarPedidoRequest request, CancellationToken ct);
    Task EliminarAsync(int id, CancellationToken ct);
}

public sealed class PedidoService(
    IPedidoRepository repositorio,
    IPedidoQueries consultas,
    ICatalogoQueries catalogos,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioActual,
    TimeProvider clock,
    IValidator<CrearPedidoRequest> validadorCrear,
    IValidator<ActualizarPedidoRequest> validadorActualizar,
    IValidator<PedidoFiltro> validadorFiltro,
    ILogger<PedidoService> logger) : IPedidoService
{
    public async Task<PagedResult<PedidoResumenDto>> ListarAsync(PedidoFiltro filtro, CancellationToken ct)
    {
        await validadorFiltro.ValidateAndThrowAsync(filtro, ct);
        return await consultas.ListarAsync(usuarioActual.EmpresaId, filtro, ct);
    }

    public async Task<PedidoDto> ObtenerAsync(int id, CancellationToken ct) =>
        await consultas.ObtenerAsync(id, usuarioActual.EmpresaId, ct)
        ?? throw new NotFoundException("Pedido", id);

    public async Task<SiguienteNumeroDto> SiguienteNumeroAsync(CancellationToken ct) =>
        new(await consultas.SiguienteNumeroAsync(ct));

    public Task<PedidoResumenEstadosDto> ResumenAsync(CancellationToken ct) =>
        consultas.ResumenAsync(usuarioActual.EmpresaId, ct);

    public async Task<PedidoDto> CrearAsync(CrearPedidoRequest request, CancellationToken ct)
    {
        await validadorCrear.ValidateAndThrowAsync(request, ct);
        await ValidarReferenciasAsync(request, clienteActual: null, productosYaEnPedido: new HashSet<int>(), ct);
        await AsegurarNumeroUnicoAsync(request.NumeroPedido, excluirId: null, ct);

        var pedido = Pedido.Crear(
            request.NumeroPedido,
            request.SucursalId,
            request.ClienteId,
            request.MonedaId,
            request.Fecha.ToDateTime(TimeOnly.MinValue),
            ALineas(request),
            usuarioActual.UsuarioId,
            Ahora());

        repositorio.Agregar(pedido);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Pedido {PedidoId} {NumeroPedido} creado por {UsuarioId} por {Total}",
            pedido.Id, pedido.NumeroPedido, usuarioActual.UsuarioId, pedido.TotalPedido);

        return await ObtenerAsync(pedido.Id, ct);
    }

    public async Task<PedidoDto> ActualizarAsync(int id, ActualizarPedidoRequest request, CancellationToken ct)
    {
        await validadorActualizar.ValidateAndThrowAsync(request, ct);

        var pedido = await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct)
                     ?? throw new NotFoundException("Pedido", id);

        if (request.RowVersion is not null && request.RowVersion != Convert.ToBase64String(pedido.RowVersion))
            throw PedidoModificadoPorOtro();

        var productosYaEnPedido = pedido.Detalles.Select(d => d.ProductoId).ToHashSet();
        await ValidarReferenciasAsync(request, pedido.ClienteId, productosYaEnPedido, ct);
        await AsegurarNumeroUnicoAsync(request.NumeroPedido, excluirId: id, ct);

        pedido.Actualizar(
            request.NumeroPedido,
            request.SucursalId,
            request.ClienteId,
            request.MonedaId,
            request.Fecha.ToDateTime(TimeOnly.MinValue),
            Enum.Parse<EstadoPedido>(request.Estado, ignoreCase: true),
            ALineas(request),
            usuarioActual.UsuarioId,
            Ahora());

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Pedido {PedidoId} actualizado por {UsuarioId}: estado {Estado}, total {Total}",
            id, usuarioActual.UsuarioId, pedido.Estado, pedido.TotalPedido);

        return await ObtenerAsync(id, ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        var pedido = await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct)
                     ?? throw new NotFoundException("Pedido", id);

        pedido.Anular(usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Pedido {PedidoId} {NumeroPedido} anulado por {UsuarioId}",
            id, pedido.NumeroPedido, usuarioActual.UsuarioId);
    }

    private async Task AsegurarNumeroUnicoAsync(string numeroPedido, int? excluirId, CancellationToken ct)
    {
        var numero = Pedido.NormalizarNumero(numeroPedido);
        // Incluye pedidos anulados: el número queda reservado para no confundir documentos ya emitidos.
        if (await repositorio.ExisteNumeroAsync(numero, excluirId, ct))
            throw new ConflictException("PEDIDO_NUMERO_DUPLICADO", $"Ya existe un pedido con el número {numero}.");
    }

    /// <summary>
    /// Todo lo que el pedido referencia debe existir y ser de la empresa del usuario. El cliente y los
    /// productos que el pedido ya tenía no se revalidan: desactivar o eliminar un maestro no debe
    /// bloquear la edición de los pedidos que ya lo usan.
    /// </summary>
    private async Task ValidarReferenciasAsync(PedidoRequestBase request, int? clienteActual,
        IReadOnlySet<int> productosYaEnPedido, CancellationToken ct)
    {
        var empresaId = usuarioActual.EmpresaId;

        if (request.ClienteId != clienteActual && !await catalogos.ClienteValidoAsync(empresaId, request.ClienteId, ct))
            throw new BusinessRuleException("CLIENTE_INVALIDO", "El cliente no existe o no está activo.");
        if (!await catalogos.SucursalValidaAsync(empresaId, request.SucursalId, ct))
            throw new BusinessRuleException("SUCURSAL_INVALIDA", "La sucursal no existe o no está activa.");
        if (!await catalogos.MonedaValidaAsync(request.MonedaId, ct))
            throw new BusinessRuleException("MONEDA_INVALIDA", "La moneda no existe o no está activa.");

        var porValidar = request.Detalles.Select(d => d.ProductoId)
            .Where(p => !productosYaEnPedido.Contains(p))
            .Distinct()
            .ToList();
        if (porValidar.Count == 0)
            return;

        var validos = await catalogos.ProductosValidosAsync(empresaId, porValidar, ct);
        var invalidos = porValidar.Where(p => !validos.Contains(p)).ToList();
        if (invalidos.Count > 0)
            throw new BusinessRuleException("PRODUCTO_INVALIDO",
                $"Producto(s) inexistente(s) o inactivo(s): {string.Join(", ", invalidos)}.");
    }

    private static List<LineaPedido> ALineas(PedidoRequestBase request) =>
        request.Detalles
            .Select(d => new LineaPedido(d.Id, d.ProductoId, d.Cantidad, d.PrecioVenta, d.Descuento))
            .ToList();

    private DateTime Ahora() => clock.GetLocalNow().DateTime;

    private static ConflictException PedidoModificadoPorOtro() =>
        new("PEDIDO_MODIFICADO", "Otro usuario modificó el pedido mientras lo editaba. Recargue los datos e intente nuevamente.");
}
