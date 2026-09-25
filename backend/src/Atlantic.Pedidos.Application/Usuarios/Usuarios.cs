using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Auth;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Usuarios;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlantic.Pedidos.Application.Usuarios;

public sealed record UsuarioAdminDto(
    int Id,
    string NombreUsuario,
    string NombreCompleto,
    string NumeroDocumento,
    string Email,
    string Rol,
    bool Activo,
    bool Bloqueado,
    int IntentosFallidos,
    DateTime? FechaUltimoLogin,
    DateTime? FechaCreacion);

public sealed record CrearUsuarioRequest
{
    public string NombreUsuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string NumeroDocumento { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Rol { get; init; } = Roles.User;
    public string Clave { get; init; } = string.Empty;
}

public sealed record ActualizarUsuarioRequest
{
    public string NombreCompleto { get; init; } = string.Empty;
    public string NumeroDocumento { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Rol { get; init; } = Roles.User;
    public bool Activo { get; init; } = true;
}

public sealed record RestablecerClaveRequest(string NuevaClave);

/// <summary>Estado de la cuenta que se contrasta con el token en cada petición.</summary>
public sealed record EstadoSesion(bool Activo, bool Bloqueado, DateTime? FechaBloqueo, string Rol, int? EmpresaId, DateTime? FechaUltimoCambiarClave);

internal static class ReglasClave
{
    // BCrypt sólo considera los primeros 72 bytes: una clave más larga daría una falsa sensación de seguridad.
    public static IRuleBuilderOptions<T, string> ClaveSegura<T>(this IRuleBuilder<T, string> regla) =>
        regla
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(72).WithMessage("La contraseña admite como máximo 72 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir un número.");

    public static IRuleBuilderOptions<T, string> RolValido<T>(this IRuleBuilder<T, string> regla) =>
        regla.Must(r => Roles.Todos.Contains(r)).WithMessage($"Rol no válido. Valores: {string.Join(", ", Roles.Todos)}.");
}

public sealed class CrearUsuarioRequestValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioRequestValidator()
    {
        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("El usuario sólo admite letras, números, punto, guion y guion bajo.");
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(300);
        RuleFor(x => x.NumeroDocumento).NotEmpty().WithMessage("El documento es obligatorio.").MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().WithMessage("El email es obligatorio.").EmailAddress().WithMessage("El email no es válido.").MaximumLength(300);
        RuleFor(x => x.Rol).RolValido();
        RuleFor(x => x.Clave).ClaveSegura();
    }
}

public sealed class ActualizarUsuarioRequestValidator : AbstractValidator<ActualizarUsuarioRequest>
{
    public ActualizarUsuarioRequestValidator()
    {
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(300);
        RuleFor(x => x.NumeroDocumento).NotEmpty().WithMessage("El documento es obligatorio.").MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().WithMessage("El email es obligatorio.").EmailAddress().WithMessage("El email no es válido.").MaximumLength(300);
        RuleFor(x => x.Rol).RolValido();
    }
}

public sealed class RestablecerClaveRequestValidator : AbstractValidator<RestablecerClaveRequest>
{
    public RestablecerClaveRequestValidator() => RuleFor(x => x.NuevaClave).ClaveSegura();
}

public interface IUsuarioAdminService
{
    Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(string? buscar, CancellationToken ct);
    Task<UsuarioAdminDto> CrearAsync(CrearUsuarioRequest request, CancellationToken ct);
    Task<UsuarioAdminDto> ActualizarAsync(int id, ActualizarUsuarioRequest request, CancellationToken ct);
    Task<UsuarioAdminDto> DesbloquearAsync(int id, CancellationToken ct);
    Task RestablecerClaveAsync(int id, RestablecerClaveRequest request, CancellationToken ct);
}

public sealed class UsuarioAdminService(
    IUsuarioRepository repositorio,
    IUsuarioQueries consultas,
    IEstadoSesionProvider sesiones,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioActual,
    TimeProvider clock,
    IValidator<CrearUsuarioRequest> validadorCrear,
    IValidator<ActualizarUsuarioRequest> validadorActualizar,
    IValidator<RestablecerClaveRequest> validadorClave,
    ILogger<UsuarioAdminService> logger) : IUsuarioAdminService
{
    public Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(string? buscar, CancellationToken ct) =>
        consultas.ListarAsync(usuarioActual.EmpresaId, buscar, ct);

    public async Task<UsuarioAdminDto> CrearAsync(CrearUsuarioRequest request, CancellationToken ct)
    {
        await validadorCrear.ValidateAndThrowAsync(request, ct);
        await AsegurarUnicosAsync(request.Email, request.NombreUsuario, excluirId: null, ct);

        // IDUsuario no es identity en el modelo referencial. MAX+1 es aceptable para un alta manual de
        // administrador; si dos altas chocan, la PK lo detiene y UnitOfWork responde 409 (reintentar).
        var id = await repositorio.SiguienteIdAsync(ct);
        var usuario = Usuario.Crear(id, usuarioActual.EmpresaId, request.NombreUsuario, request.NumeroDocumento,
            request.Email, request.NombreCompleto, request.Rol, hasher.Hash(request.Clave), usuarioActual.UsuarioId, Ahora());

        repositorio.Agregar(usuario);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Usuario {UsuarioId} ({Email}) creado con rol {Rol} por {AdminId}",
            usuario.Id, usuario.Email, usuario.Rol, usuarioActual.UsuarioId);
        return await ObtenerAsync(usuario.Id, ct);
    }

    public async Task<UsuarioAdminDto> ActualizarAsync(int id, ActualizarUsuarioRequest request, CancellationToken ct)
    {
        await validadorActualizar.ValidateAndThrowAsync(request, ct);
        var usuario = await CargarAsync(id, ct);

        var pierdeAdmin = usuario.Rol == Roles.Admin && usuario.EstaActivo && (request.Rol != Roles.Admin || !request.Activo);
        if (pierdeAdmin && id == usuarioActual.UsuarioId)
            throw new BusinessRuleException("USUARIO_AUTOGESTION",
                "No puede quitarse a sí mismo el rol Admin ni desactivar su propia cuenta.");
        if (pierdeAdmin && await repositorio.ContarAdminsActivosAsync(usuarioActual.EmpresaId, excluirId: id, ct) == 0)
            throw new BusinessRuleException("ULTIMO_ADMIN", "La empresa debe conservar al menos un administrador activo.");

        await AsegurarUnicosAsync(request.Email, nombreUsuario: null, excluirId: id, ct);

        usuario.Actualizar(request.NombreCompleto, request.NumeroDocumento, request.Email, request.Rol, request.Activo,
            usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);
        sesiones.Invalidar(id);

        logger.LogInformation("Usuario {UsuarioId} actualizado por {AdminId}: rol {Rol}, activo {Activo}",
            id, usuarioActual.UsuarioId, request.Rol, request.Activo);
        return await ObtenerAsync(id, ct);
    }

    public async Task<UsuarioAdminDto> DesbloquearAsync(int id, CancellationToken ct)
    {
        var usuario = await CargarAsync(id, ct);
        usuario.Desbloquear(usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);
        sesiones.Invalidar(id);

        logger.LogInformation("Usuario {UsuarioId} desbloqueado por {AdminId}", id, usuarioActual.UsuarioId);
        return await ObtenerAsync(id, ct);
    }

    public async Task RestablecerClaveAsync(int id, RestablecerClaveRequest request, CancellationToken ct)
    {
        await validadorClave.ValidateAndThrowAsync(request, ct);
        var usuario = await CargarAsync(id, ct);

        usuario.CambiarClave(hasher.Hash(request.NuevaClave), usuarioActual.UsuarioId, Ahora());
        await unitOfWork.SaveChangesAsync(ct);
        sesiones.Invalidar(id);

        logger.LogInformation("Clave del usuario {UsuarioId} restablecida por {AdminId}; sus sesiones anteriores quedan revocadas",
            id, usuarioActual.UsuarioId);
    }

    private async Task<Usuario> CargarAsync(int id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, usuarioActual.EmpresaId, ct) ?? throw new NotFoundException("Usuario", id);

    private async Task<UsuarioAdminDto> ObtenerAsync(int id, CancellationToken ct) =>
        (await consultas.ListarAsync(usuarioActual.EmpresaId, null, ct)).FirstOrDefault(u => u.Id == id)
        ?? throw new NotFoundException("Usuario", id);

    private async Task AsegurarUnicosAsync(string email, string? nombreUsuario, int? excluirId, CancellationToken ct)
    {
        if (await repositorio.ExisteEmailAsync(Usuario.NormalizarEmail(email), excluirId, ct))
            throw new ConflictException("USUARIO_EMAIL_DUPLICADO", $"Ya existe un usuario con el email {email.Trim()}.");
        if (nombreUsuario is not null && await repositorio.ExisteNombreUsuarioAsync(nombreUsuario.Trim(), excluirId, ct))
            throw new ConflictException("USUARIO_NOMBRE_DUPLICADO", $"El usuario {nombreUsuario.Trim()} ya existe.");
    }

    private DateTime Ahora() => clock.GetLocalNow().DateTime;
}

/// <summary>
/// Control de sesión en cada petición autenticada. Un JWT es válido hasta que vence, pero el administrador
/// puede desactivar, bloquear, cambiar el rol o la clave de un usuario: estas reglas lo reflejan de inmediato.
/// </summary>
public interface IValidadorSesion
{
    /// <returns>Motivo del rechazo, o null si la sesión sigue siendo válida.</returns>
    Task<string?> ValidarAsync(int usuarioId, int empresaId, string rol, DateTime emitidoUtc, CancellationToken ct);
}

public sealed class ValidadorSesion(IEstadoSesionProvider sesiones, TimeProvider clock, IOptions<LockoutOptions> lockout) : IValidadorSesion
{
    public async Task<string?> ValidarAsync(int usuarioId, int empresaId, string rol, DateTime emitidoUtc, CancellationToken ct)
    {
        var estado = await sesiones.ObtenerAsync(usuarioId, ct);
        var ahora = clock.GetLocalNow().DateTime;

        if (estado is null || !estado.Activo)
            return "Tu acceso fue deshabilitado por un administrador.";
        if (estado.EmpresaId != empresaId)
            return "Tu usuario cambió de empresa. Vuelve a iniciar sesión.";
        if (estado.Bloqueado && (estado.FechaBloqueo is null || estado.FechaBloqueo.Value.AddMinutes(lockout.Value.MinutosBloqueo) > ahora))
            return "Tu cuenta está bloqueada.";
        if (!string.Equals(estado.Rol, rol, StringComparison.Ordinal))
            return "Tus permisos cambiaron. Vuelve a iniciar sesión.";

        if (estado.FechaUltimoCambiarClave is { } cambio)
        {
            // La BD guarda hora local en datetime (redondea a ~3 ms, de ahí el margen de 5 ms); la emisión del
            // token llega en UTC con milisegundos.
            var cambioUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(cambio, DateTimeKind.Unspecified), clock.LocalTimeZone);
            if (emitidoUtc.AddMilliseconds(5) < cambioUtc)
                return "Tu contraseña fue restablecida. Vuelve a iniciar sesión.";
        }

        return null;
    }
}
