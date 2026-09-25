using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace Atlantic.Pedidos.Application.Clientes;

public sealed record ClienteResumenDto(
    int Id,
    string TipoDocumento,
    string NumeroDocumento,
    string Nombre,
    string? Celular,
    bool Activo,
    int PedidosEnCurso);

public sealed record ClienteDetalleDto(
    int Id,
    int TipoDocumentoId,
    string TipoDocumento,
    string NumeroDocumento,
    string? PrimerNombre,
    string? SegundoNombre,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? RazonSocial,
    string? NombreComercial,
    string? Direccion,
    string? Celular,
    bool Activo,
    DateTime? FechaCreacion,
    DateTime? FechaModificacion);

public sealed record GuardarClienteRequest
{
    public int TipoDocumentoId { get; init; }
    public string NumeroDocumento { get; init; } = string.Empty;
    public string? PrimerNombre { get; init; }
    public string? SegundoNombre { get; init; }
    public string? ApellidoPaterno { get; init; }
    public string? ApellidoMaterno { get; init; }
    public string? RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public string? Direccion { get; init; }
    public string? Celular { get; init; }
    public bool Activo { get; init; } = true;
}

public sealed class GuardarClienteRequestValidator : AbstractValidator<GuardarClienteRequest>
{
    public GuardarClienteRequestValidator()
    {
        RuleFor(x => x.TipoDocumentoId).GreaterThan(0).WithMessage("Seleccione el tipo de documento.");
        RuleFor(x => x.NumeroDocumento).NotEmpty().WithMessage("El número de documento es obligatorio.").MaximumLength(20);
        RuleFor(x => x.PrimerNombre).MaximumLength(100);
        RuleFor(x => x.SegundoNombre).MaximumLength(100);
        RuleFor(x => x.ApellidoPaterno).MaximumLength(100);
        RuleFor(x => x.ApellidoMaterno).MaximumLength(100);
        RuleFor(x => x.RazonSocial).MaximumLength(200);
        RuleFor(x => x.NombreComercial).MaximumLength(100);
        RuleFor(x => x.Direccion).MaximumLength(200);
        RuleFor(x => x.Celular)
            .MaximumLength(20)
            .Matches(@"^[0-9+\-\s]*$").WithMessage("El celular sólo admite números, espacios, + y -.");
    }
}

public interface IClienteService
{
    Task<PagedResult<ClienteResumenDto>> ListarAsync(MaestroFiltro filtro, CancellationToken ct);
    Task<ClienteDetalleDto> ObtenerAsync(int id, CancellationToken ct);
    Task<ClienteDetalleDto> CrearAsync(GuardarClienteRequest request, CancellationToken ct);
    Task<ClienteDetalleDto> ActualizarAsync(int id, GuardarClienteRequest request, CancellationToken ct);
    Task EliminarAsync(int id, CancellationToken ct);
}

public sealed class ClienteService(
    IClienteRepository repositorio,
    IClienteQueries consultas,
    ICatalogoQueries catalogos,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioActual,
    TimeProvider clock,
    IValidator<GuardarClienteRequest> validador,
    IValidator<MaestroFiltro> validadorFiltro,
    ILogger<ClienteService> logger) : IClienteService
{
    public async Task<PagedResult<ClienteResumenDto>> ListarAsync(MaestroFiltro filtro, CancellationToken ct)
    {
        await validadorFiltro.ValidateAndThrowAsync(filtro, ct);
        return await consultas.ListarAsync(usuarioActual.EmpresaId, filtro, ct);
    }

    public async Task<ClienteDetalleDto> ObtenerAsync(int id, CancellationToken ct) =>
        await consultas.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Cliente", id);

    public async Task<ClienteDetalleDto> CrearAsync(GuardarClienteRequest request, CancellationToken ct)
    {
        var tipo = await ValidarAsync(request, excluirId: null, ct);

        var cliente = Cliente.Crear(usuarioActual.EmpresaId, tipo, ADatos(request), usuarioActual.UsuarioId, Ahora());
        repositorio.Agregar(cliente);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Cliente {ClienteId} {Documento} creado por {UsuarioId}",
            cliente.Id, cliente.NumeroDocumento, usuarioActual.UsuarioId);
        return await ObtenerAsync(cliente.Id, ct);
    }

    public async Task<ClienteDetalleDto> ActualizarAsync(int id, GuardarClienteRequest request, CancellationToken ct)
    {
        var cliente = await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Cliente", id);
        var tipo = await ValidarAsync(request, excluirId: id, ct);

        cliente.Actualizar(tipo, ADatos(request), usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Cliente {ClienteId} actualizado por {UsuarioId}", id, usuarioActual.UsuarioId);
        return await ObtenerAsync(id, ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        var cliente = await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Cliente", id);

        var enCurso = await consultas.PedidosEnCursoAsync(id, ct);
        if (enCurso > 0)
            throw new BusinessRuleException("CLIENTE_CON_PEDIDOS_EN_CURSO",
                $"El cliente tiene {enCurso} pedido(s) en curso. Entréguelos o elimínelos antes de eliminar el cliente.");

        cliente.Eliminar(usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Cliente {ClienteId} eliminado por {UsuarioId}", id, usuarioActual.UsuarioId);
    }

    /// <summary>
    /// Forma (FluentValidation) + reglas que dependen de la BD. Todas salen como error de campo (400)
    /// para que el formulario las pinte junto al input.
    /// </summary>
    private async Task<TipoDocumento> ValidarAsync(GuardarClienteRequest request, int? excluirId, CancellationToken ct)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        var tipo = await catalogos.TipoDocumentoAsync(request.TipoDocumentoId, ct)
                   ?? throw ErrorDeCampo("tipoDocumentoId", "El tipo de documento no existe.");

        if (Cliente.ValidarDocumento(tipo.Abreviatura, request.NumeroDocumento) is { } error)
            throw ErrorDeCampo("numeroDocumento", error);

        var faltantes = new List<ValidationFailure>();
        if (Cliente.EsTipoEmpresa(tipo.Abreviatura))
        {
            if (string.IsNullOrWhiteSpace(request.RazonSocial))
                faltantes.Add(new("razonSocial", "La razón social es obligatoria para un RUC."));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.PrimerNombre))
                faltantes.Add(new("primerNombre", "El nombre es obligatorio."));
            if (string.IsNullOrWhiteSpace(request.ApellidoPaterno))
                faltantes.Add(new("apellidoPaterno", "El apellido paterno es obligatorio."));
        }
        if (faltantes.Count > 0)
            throw new ValidationException(faltantes);

        if (await repositorio.ExisteDocumentoAsync(usuarioActual.EmpresaId, tipo.Id, request.NumeroDocumento.Trim(), excluirId, ct))
            throw new ConflictException("CLIENTE_DOCUMENTO_DUPLICADO",
                $"Ya existe un cliente con {tipo.Abreviatura} {request.NumeroDocumento.Trim()}.");

        return tipo;
    }

    private static ValidationException ErrorDeCampo(string campo, string mensaje) => new([new ValidationFailure(campo, mensaje)]);

    private static DatosCliente ADatos(GuardarClienteRequest r) => new(r.NumeroDocumento, r.PrimerNombre, r.SegundoNombre,
        r.ApellidoPaterno, r.ApellidoMaterno, r.RazonSocial, r.NombreComercial, r.Direccion, r.Celular, r.Activo);

    private DateTime Ahora() => clock.GetLocalNow().DateTime;
}
