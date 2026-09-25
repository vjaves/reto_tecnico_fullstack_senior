using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Atlantic.Pedidos.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UsuarioSesionDto(int Id, string Nombre, string Email, string Rol);

/// <summary>
/// <c>token</c> y <c>expiresIn</c> son el contrato pedido; <c>tokenType</c> y <c>usuario</c> son adicionales
/// para que el front no tenga que decodificar el JWT para pintar el menú.
/// </summary>
public sealed record LoginResponse(string Token, int ExpiresIn, string TokenType, UsuarioSesionDto Usuario);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(300);
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(100);
    }
}

public sealed class LockoutOptions
{
    public const string Section = "Lockout";
    public int MaxIntentosFallidos { get; init; } = 5;
    public int MinutosBloqueo { get; init; } = 15;
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
}

public sealed class AuthService(
    IUsuarioRepository usuarios,
    IPasswordHasher hasher,
    IJwtTokenGenerator tokens,
    IUnitOfWork unitOfWork,
    IValidator<LoginRequest> validator,
    TimeProvider clock,
    Microsoft.Extensions.Options.IOptions<LockoutOptions> lockoutOptions,
    ILogger<AuthService> logger) : IAuthService
{
    // Hash válido de una clave aleatoria: si el email no existe igual se ejecuta un Verify,
    // para que el tiempo de respuesta no delate qué correos están registrados.
    private const string HashSenuelo = "$2a$11$HeQwDFhlbvd9ydTwh96OHuJ8O8CDq7ud6AzxGJIvDfovScNYkDJ.C";

    private readonly LockoutOptions _lockout = lockoutOptions.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var ahora = clock.GetLocalNow().DateTime;
        var duracionBloqueo = TimeSpan.FromMinutes(_lockout.MinutosBloqueo);
        var usuario = await usuarios.ObtenerPorEmailAsync(request.Email.Trim(), ct);

        if (usuario is null || !usuario.EstaActivo)
        {
            hasher.Verify(request.Password, HashSenuelo);
            logger.LogWarning("Login rechazado: {Email} no existe o está dado de baja", request.Email);
            throw CredencialesInvalidas();
        }

        if (usuario.EstaBloqueado(ahora, duracionBloqueo))
        {
            logger.LogWarning("Login rechazado: usuario {UsuarioId} bloqueado", usuario.Id);
            throw new AuthenticationFailedException("USUARIO_BLOQUEADO",
                $"La cuenta está bloqueada por intentos fallidos. Intente nuevamente en {_lockout.MinutosBloqueo} minutos.");
        }

        if (!hasher.Verify(request.Password, usuario.ClaveHash))
        {
            usuario.RegistrarLoginFallido(ahora, _lockout.MaxIntentosFallidos, duracionBloqueo);
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogWarning("Login fallido para usuario {UsuarioId}, intento {Intento}",
                usuario.Id, usuario.NumIntentoFallidoLogin);
            throw CredencialesInvalidas();
        }

        if (usuario.EmpresaId is null)
            throw new AuthenticationFailedException("USUARIO_SIN_EMPRESA", "El usuario no tiene una empresa asignada.");

        usuario.RegistrarLoginExitoso(ahora);
        await unitOfWork.SaveChangesAsync(ct);

        var token = tokens.Generar(usuario);
        logger.LogInformation("Login correcto para usuario {UsuarioId} con rol {Rol}", usuario.Id, usuario.Rol);

        return new LoginResponse(token.Token, token.ExpiresInSeconds, "Bearer",
            new UsuarioSesionDto(usuario.Id, usuario.NombreCompleto, usuario.Email, usuario.Rol));
    }

    private static AuthenticationFailedException CredencialesInvalidas() =>
        new("CREDENCIALES_INVALIDAS", "Email o contraseña incorrectos.");
}
