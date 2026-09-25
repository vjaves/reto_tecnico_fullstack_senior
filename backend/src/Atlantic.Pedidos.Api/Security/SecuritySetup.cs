using System.Threading.RateLimiting;
using Atlantic.Pedidos.Application.Usuarios;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Infrastructure.Resilience;
using Atlantic.Pedidos.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Atlantic.Pedidos.Api.Security;

public static class AuthPolicies
{
    public const string SoloAdmin = nameof(SoloAdmin);
}

public static class RateLimitPolicies
{
    public const string Login = nameof(Login);
}

/// <summary>El token es válido criptográficamente pero la cuenta ya no lo respalda.</summary>
public sealed class SesionRevocadaException(string message) : Exception(message);

public static class SecuritySetup
{
    public static IServiceCollection AddJwtSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Falta la sección Jwt en la configuración.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // conserva "sub", "role", "empresa_id" tal cual
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.ObtenerClave(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaims.Name,
                    RoleClaimType = AppClaims.Role
                };
                options.Events = new JwtBearerEvents
                {
                    // Control de sesión: la firma y la vigencia no bastan. Se contrasta el token con el estado
                    // actual de la cuenta (activa, no bloqueada, mismo rol, clave no restablecida después).
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        if (!int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var usuarioId)
                            || !int.TryParse(principal.FindFirst(AppClaims.EmpresaId)?.Value, out var empresaId))
                        {
                            context.Fail(new SesionRevocadaException("El token no identifica al usuario."));
                            return;
                        }

                        var emitido = long.TryParse(principal.FindFirst(AppClaims.EmitidoMs)?.Value, out var ms)
                            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                            : context.SecurityToken is JsonWebToken jwt ? jwt.IssuedAt : DateTime.MinValue;
                        var servicios = context.HttpContext.RequestServices;
                        try
                        {
                            var motivo = await servicios.GetRequiredService<IValidadorSesion>().ValidarAsync(usuarioId, empresaId,
                                principal.FindFirst(AppClaims.Role)?.Value ?? string.Empty, emitido, context.HttpContext.RequestAborted);

                            if (motivo is not null)
                                context.Fail(new SesionRevocadaException(motivo));
                        }
                        catch (Exception ex) when (SqlResilience.EsFalloDeInfraestructura(ex))
                        {
                            // Si la BD no responde, no se expulsa a todos los usuarios con un 401: la firma y la
                            // vigencia del token ya se validaron. La petición sigue y, si necesita la BD,
                            // termina en 503 (reintentable) en lugar de cerrar la sesión.
                            servicios.GetRequiredService<ILoggerFactory>().CreateLogger("Sesion")
                                .LogWarning("Control de sesión omitido para {UsuarioId}: BD no disponible ({Error})", usuarioId, ex.GetType().Name);
                        }
                    },
                    // Sin esto, un 401/403 sale con cuerpo vacío y el front no sabe si el token venció.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var (code, detail) = context.AuthenticateFailure switch
                        {
                            SecurityTokenExpiredException => ("TOKEN_EXPIRADO", "La sesión expiró. Inicie sesión nuevamente."),
                            SesionRevocadaException revocada => ("SESION_REVOCADA", revocada.Message),
                            _ => ("TOKEN_REQUERIDO", "Se requiere un token Bearer válido.")
                        };
                        await EscribirProblema(context.HttpContext, StatusCodes.Status401Unauthorized, "No autenticado", code, detail);
                    },
                    OnForbidden = context => EscribirProblema(context.HttpContext, StatusCodes.Status403Forbidden,
                        "Acceso denegado", "ROL_INSUFICIENTE", "Su rol no permite realizar esta acción.")
                };
            });

        services.AddAuthorizationBuilder()
            // Seguro por defecto: todo endpoint exige token salvo que declare [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthPolicies.SoloAdmin, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Límite global de la API: token bucket por usuario autenticado (o por IP si no lo hay).
            // Permite ráfagas cortas (pantallas que cargan varios catálogos a la vez) pero corta el abuso sostenido.
            var apiPorMinuto = configuration.GetValue("RateLimit:ApiPorMinuto", 300);
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (http.Request.Path.StartsWithSegments("/health") || http.Request.Path.StartsWithSegments("/swagger"))
                    return RateLimitPartition.GetNoLimiter("sin-limite");

                var clave = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { } sub
                    ? $"usuario:{sub}"
                    : $"ip:{http.Connection.RemoteIpAddress}";
                return RateLimitPartition.GetTokenBucketLimiter(clave, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = apiPorMinuto,
                    TokensPerPeriod = Math.Max(apiPorMinuto / 6, 1),
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Login: ventana fija estricta por IP. Frena la fuerza bruta distribuida entre cuentas;
            // el bloqueo por cuenta lo hace el dominio.
            options.AddPolicy(RateLimitPolicies.Login, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("RateLimit:LoginPorMinuto", 10),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                var esLogin = context.HttpContext.Request.Path.StartsWithSegments("/auth");
                return new ValueTask(EscribirProblema(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "Demasiadas solicitudes", "LIMITE_EXCEDIDO",
                    esLogin
                        ? "Demasiados intentos de inicio de sesión. Espere un minuto."
                        : "Demasiadas solicitudes en poco tiempo. Espere unos segundos e intente nuevamente."));
            };
        });

        return services;
    }

    private static Task EscribirProblema(HttpContext http, int status, string title, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = http.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = http.TraceIdentifier;

        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json");
    }
}
