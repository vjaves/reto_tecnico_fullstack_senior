using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace Atlantic.Pedidos.Application.Productos;

public sealed record ProductoResumenDto(
    int Id,
    string? Codigo,
    string Nombre,
    string? UnidadMedida,
    bool Activo,
    int PedidosEnCurso);

public sealed record ProductoDetalleDto(
    int Id,
    string? Codigo,
    string Nombre,
    string? Descripcion,
    int? UnidadMedidaId,
    string? UnidadMedida,
    bool Activo,
    DateTime FechaCreacion,
    DateTime? FechaModificacion);

public sealed record GuardarProductoRequest
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public int UnidadMedidaId { get; init; }
    public bool Activo { get; init; } = true;
}

public sealed class GuardarProductoRequestValidator : AbstractValidator<GuardarProductoRequest>
{
    public GuardarProductoRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("El código sólo admite letras, números, punto, guion y guion bajo.");
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(300).WithMessage("El nombre admite como máximo 300 caracteres.");
        RuleFor(x => x.Descripcion).MaximumLength(2000);
        RuleFor(x => x.UnidadMedidaId).GreaterThan(0).WithMessage("Seleccione la unidad de medida.");
    }
}

public interface IProductoService
{
    Task<PagedResult<ProductoResumenDto>> ListarAsync(MaestroFiltro filtro, CancellationToken ct);
    Task<ProductoDetalleDto> ObtenerAsync(int id, CancellationToken ct);
    Task<ProductoDetalleDto> CrearAsync(GuardarProductoRequest request, CancellationToken ct);
    Task<ProductoDetalleDto> ActualizarAsync(int id, GuardarProductoRequest request, CancellationToken ct);
    Task EliminarAsync(int id, CancellationToken ct);
}

public sealed class ProductoService(
    IProductoRepository repositorio,
    IProductoQueries consultas,
    ICatalogoQueries catalogos,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioActual,
    TimeProvider clock,
    IValidator<GuardarProductoRequest> validador,
    IValidator<MaestroFiltro> validadorFiltro,
    ILogger<ProductoService> logger) : IProductoService
{
    public async Task<PagedResult<ProductoResumenDto>> ListarAsync(MaestroFiltro filtro, CancellationToken ct)
    {
        await validadorFiltro.ValidateAndThrowAsync(filtro, ct);
        return await consultas.ListarAsync(usuarioActual.EmpresaId, filtro, ct);
    }

    public async Task<ProductoDetalleDto> ObtenerAsync(int id, CancellationToken ct) =>
        await consultas.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Producto", id);

    public async Task<ProductoDetalleDto> CrearAsync(GuardarProductoRequest request, CancellationToken ct)
    {
        await ValidarAsync(request, excluirId: null, ct);

        var producto = Producto.Crear(usuarioActual.EmpresaId, ADatos(request), usuarioActual.UsuarioId, Ahora());
        repositorio.Agregar(producto);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Producto {ProductoId} {Codigo} creado por {UsuarioId}",
            producto.Id, producto.CodigoProducto, usuarioActual.UsuarioId);
        return await ObtenerAsync(producto.Id, ct);
    }

    public async Task<ProductoDetalleDto> ActualizarAsync(int id, GuardarProductoRequest request, CancellationToken ct)
    {
        var producto = await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Producto", id);
        await ValidarAsync(request, excluirId: id, ct);

        producto.Actualizar(ADatos(request), usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Producto {ProductoId} actualizado por {UsuarioId}", id, usuarioActual.UsuarioId);
        return await ObtenerAsync(id, ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        var producto = await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Producto", id);

        var enCurso = await consultas.PedidosEnCursoAsync(id, ct);
        if (enCurso > 0)
            throw new BusinessRuleException("PRODUCTO_CON_PEDIDOS_EN_CURSO",
                $"El producto está en {enCurso} pedido(s) en curso. Puede desactivarlo para que no se ofrezca en pedidos nuevos.");

        producto.Eliminar(usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Producto {ProductoId} eliminado por {UsuarioId}", id, usuarioActual.UsuarioId);
    }

    private async Task ValidarAsync(GuardarProductoRequest request, int? excluirId, CancellationToken ct)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        if (!await catalogos.UnidadMedidaValidaAsync(request.UnidadMedidaId, ct))
            throw new ValidationException([new ValidationFailure("unidadMedidaId", "La unidad de medida no existe.")]);

        var codigo = Producto.NormalizarCodigo(request.Codigo);
        if (await repositorio.ExisteCodigoAsync(usuarioActual.EmpresaId, codigo, excluirId, ct))
            throw new ConflictException("PRODUCTO_CODIGO_DUPLICADO", $"Ya existe un producto con el código {codigo}.");
    }

    private static DatosProducto ADatos(GuardarProductoRequest r) =>
        new(r.Codigo, r.Nombre, r.Descripcion, r.UnidadMedidaId, r.Activo);

    private DateTime Ahora() => clock.GetLocalNow().DateTime;
}
