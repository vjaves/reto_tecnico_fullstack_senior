using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Domain.Usuarios;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Atlantic.Pedidos.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; init; } = string.Empty;
    [Required] public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 exige al menos 256 bits: 32 caracteres ASCII.</summary>
    [Required, MinLength(32)] public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)] public int ExpirationMinutes { get; init; } = 60;

    public SymmetricSecurityKey ObtenerClave() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>Nombres de claims propios del token. La API los lee en CurrentUser.</summary>
public static class AppClaims
{
    public const string EmpresaId = "empresa_id";
    public const string Role = "role";
    public const string Name = "name";

    /// <summary>
    /// Emisión con precisión de milisegundos. El claim estándar "iat" sólo tiene segundos: un token emitido en
    /// el mismo segundo en que se restablece la clave sería indistinguible de uno emitido justo después.
    /// </summary>
    public const string EmitidoMs = "iat_ms";
}

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public TokenGenerado Generar(Usuario usuario)
    {
        var ahora = clock.GetUtcNow().UtcDateTime;
        var expira = ahora.AddMinutes(_options.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(_options.ObtenerClave(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(AppClaims.Name, usuario.NombreCompleto),
                new Claim(AppClaims.Role, usuario.Rol),
                new Claim(AppClaims.EmpresaId, usuario.EmpresaId!.Value.ToString()),
                new Claim(AppClaims.EmitidoMs, new DateTimeOffset(ahora).ToUnixTimeMilliseconds().ToString(), ClaimValueTypes.Integer64)
            ])
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenGenerado(token, _options.ExpirationMinutes * 60, expira);
    }
}

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Una clave guardada en texto plano o con otro formato no debe tumbar el login con un 500.
            return false;
        }
    }
}
