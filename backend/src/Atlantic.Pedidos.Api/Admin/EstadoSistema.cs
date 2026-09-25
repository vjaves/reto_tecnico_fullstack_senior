using System.Diagnostics;
using Atlantic.Pedidos.Application.Auth;
using Atlantic.Pedidos.Infrastructure.Resilience;
using Atlantic.Pedidos.Infrastructure.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

namespace Atlantic.Pedidos.Api.Admin;

/// <summary>
/// Expone el estado del circuit breaker de SQL como health check: Closed = sano, HalfOpen = degradado
/// (probando si la BD volvió), Open/Isolated = no disponible.
/// </summary>
public sealed class CircuitoSqlHealthCheck(CircuitBreakerStateProvider estado) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(estado.CircuitState switch
        {
            CircuitState.Closed => HealthCheckResult.Healthy("Circuito cerrado: las consultas llegan a SQL Server."),
            CircuitState.HalfOpen => HealthCheckResult.Degraded("Circuito semiabierto: probando si SQL Server se recuperó."),
            _ => HealthCheckResult.Unhealthy("Circuito abierto: las consultas se rechazan sin llegar a SQL Server.")
        });
}

public sealed record ChequeoDto(string Nombre, string Estado, string? Descripcion, double DuracionMs);

public sealed record EstadoSistemaDto(
    string Estado,
    string Entorno,
    string Version,
    DateTime InicioUtc,
    long SegundosActivo,
    string CircuitoSql,
    IReadOnlyList<ChequeoDto> Chequeos,
    IReadOnlyDictionary<string, string> Configuracion);

public sealed class EstadoSistemaService(
    HealthCheckService health,
    CircuitBreakerStateProvider circuito,
    IWebHostEnvironment entorno,
    IConfiguration configuracion,
    IOptions<SqlResilienceOptions> resiliencia,
    IOptions<JwtOptions> jwt,
    IOptions<LockoutOptions> lockout)
{
    private static readonly DateTime Inicio = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public async Task<EstadoSistemaDto> ObtenerAsync(CancellationToken ct)
    {
        var reporte = await health.CheckHealthAsync(ct);
        var r = resiliencia.Value;

        return new EstadoSistemaDto(
            reporte.Status.ToString(),
            entorno.EnvironmentName,
            typeof(EstadoSistemaService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Inicio,
            (long)(DateTime.UtcNow - Inicio).TotalSeconds,
            circuito.CircuitState.ToString(),
            reporte.Entries
                .Select(e => new ChequeoDto(e.Key, e.Value.Status.ToString(), e.Value.Description ?? e.Value.Exception?.Message,
                    Math.Round(e.Value.Duration.TotalMilliseconds, 1)))
                .ToList(),
            // Sólo parámetros operativos: nunca la cadena de conexión ni la clave de firma.
            new Dictionary<string, string>
            {
                ["JWT: expiración"] = $"{jwt.Value.ExpirationMinutes} min",
                ["Login: intentos antes de bloquear"] = lockout.Value.MaxIntentosFallidos.ToString(),
                ["Login: duración del bloqueo"] = $"{lockout.Value.MinutosBloqueo} min",
                ["Rate limit: login por IP"] = $"{configuracion.GetValue("RateLimit:LoginPorMinuto", 10)}/min",
                ["Rate limit: API por usuario"] = $"{configuracion.GetValue("RateLimit:ApiPorMinuto", 300)}/min",
                ["SQL: reintentos"] = $"{r.MaxReintentos} (backoff exponencial desde {r.RetrasoBaseMs} ms, con jitter)",
                ["SQL: timeout por intento"] = $"{r.TimeoutSegundos} s",
                ["SQL: apertura del circuito"] = $"≥ {r.ProporcionFallos:P0} de fallos en {r.VentanaSegundos} s (mín. {r.MinimoEjecuciones} ejecuciones)",
                ["SQL: circuito abierto durante"] = $"{r.AperturaSegundos} s"
            });
    }
}
