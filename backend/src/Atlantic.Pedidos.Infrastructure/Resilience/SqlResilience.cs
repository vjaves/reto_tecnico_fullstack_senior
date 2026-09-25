using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Atlantic.Pedidos.Infrastructure.Resilience;

public sealed class SqlResilienceOptions
{
    public const string Section = "Resilience:Sql";

    /// <summary>Reintentos tras el primer intento, sólo ante errores transitorios.</summary>
    [Range(1, 10)] public int MaxReintentos { get; init; } = 3;

    /// <summary>Retraso base del backoff exponencial (con jitter).</summary>
    [Range(10, 10_000)] public int RetrasoBaseMs { get; init; } = 200;

    /// <summary>Tiempo máximo de cada intento.</summary>
    [Range(1, 300)] public int TimeoutSegundos { get; init; } = 30;

    /// <summary>Proporción de fallos, dentro de la ventana, que abre el circuito.</summary>
    [Range(0.01, 1.0)] public double ProporcionFallos { get; init; } = 0.5;

    /// <summary>
    /// Mínimo de ejecuciones en la ventana antes de evaluar la proporción. Debe alcanzarse aun cuando cada fallo
    /// es lento (una conexión rechazada tarda varios segundos): con 8 en 30 s el circuito nunca se abría con
    /// tráfico bajo.
    /// </summary>
    [Range(2, 1000)] public int MinimoEjecuciones { get; init; } = 5;

    [Range(1, 600)] public int VentanaSegundos { get; init; } = 60;

    /// <summary>Tiempo que el circuito queda abierto (respondiendo 503 sin tocar la BD).</summary>
    [Range(1, 600)] public int AperturaSegundos { get; init; } = 20;
}

/// <summary>
/// Pipeline de resiliencia de SQL Server (Polly v8), de afuera hacia adentro:
/// Reintento (backoff exponencial + jitter) → Circuit breaker → Timeout por intento.
/// Una sola instancia (singleton) para que el estado del circuito sea compartido por todas las peticiones.
/// </summary>
public static class SqlResilience
{
    public const string Pipeline = "sql-server";

    // Errores que se resuelven solos si se reintenta: timeout (-2), deadlock (1205), bloqueo (1222),
    // red/transporte (20, 64, 121, 233, 10053, 10054, 10060) y throttling/failover de Azure SQL (40xxx, 49xxx).
    // Lista propia: el detector de EF vive en un namespace interno (Storage.Internal) y puede cambiar sin aviso.
    private static readonly HashSet<int> ErroresTransitorios =
        [-2, 20, 64, 121, 233, 1205, 1222, 4221, 10053, 10054, 10060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920];

    // Errores de conexión que no conviene reintentar (el servidor no existe o no responde), pero sí indican
    // que la BD está caída: cuentan para abrir el circuito.
    private static readonly HashSet<int> ErroresDeConexion = [-1, 2, 53, 4060, 10061];

    /// <summary>Se reintenta: el mismo error suele desaparecer en milisegundos (deadlock, timeout, failover).</summary>
    public static bool EsTransitorio(Exception ex) => ex switch
    {
        TimeoutRejectedException or TimeoutException => true,
        SqlException sql => sql.Errors.Cast<SqlError>().Any(e => ErroresTransitorios.Contains(e.Number)),
        DbUpdateException { InnerException: { } inner } => EsTransitorio(inner),
        _ => false
    };

    /// <summary>Cuenta como fallo de infraestructura para el circuito (y para responder 503 en vez de 500).</summary>
    public static bool EsFalloDeInfraestructura(Exception ex) => ex switch
    {
        BrokenCircuitException => true,
        SqlException sql when ErroresDeConexion.Contains(sql.Number) => true,
        DbUpdateException { InnerException: { } inner } => EsFalloDeInfraestructura(inner),
        InvalidOperationException { InnerException: { } inner } => EsFalloDeInfraestructura(inner),
        _ => EsTransitorio(ex)
    };

    public static void Configurar(ResiliencePipelineBuilder builder, SqlResilienceOptions o, CircuitBreakerStateProvider? estado = null)
    {
        builder
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(EsTransitorio),
                MaxRetryAttempts = o.MaxReintentos,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(o.RetrasoBaseMs)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(EsFalloDeInfraestructura),
                FailureRatio = o.ProporcionFallos,
                MinimumThroughput = o.MinimoEjecuciones,
                SamplingDuration = TimeSpan.FromSeconds(o.VentanaSegundos),
                BreakDuration = TimeSpan.FromSeconds(o.AperturaSegundos),
                StateProvider = estado
            })
            .AddTimeout(TimeSpan.FromSeconds(o.TimeoutSegundos));
    }
}

/// <summary>
/// Estrategia de ejecución de EF Core respaldada por el pipeline de Polly. Reemplaza a EnableRetryOnFailure:
/// EF la usa para toda consulta, SaveChanges y migración, así la resiliencia no depende de cada repositorio.
/// </summary>
internal sealed class PollyExecutionStrategy(ExecutionStrategyDependencies dependencies, ResiliencePipeline pipeline) : IExecutionStrategy
{
    // EF anida estrategias (p. ej. SaveChanges dentro de otra operación). Sólo el nivel externo pasa por
    // el pipeline; si no, los reintentos se multiplicarían y el circuito contaría dos veces el mismo fallo.
    private static readonly AsyncLocal<bool> EnEjecucion = new();

    public bool RetriesOnFailure => true;

    private DbContext Contexto => dependencies.CurrentContext.Context;

    // Dentro de una transacción explícita no se reintenta: repetir sólo una parte de ella no es seguro.
    private bool EjecutarDirecto => EnEjecucion.Value || Contexto.Database.CurrentTransaction is not null;

    public TResult Execute<TState, TResult>(TState state, Func<DbContext, TState, TResult> operation,
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
    {
        if (EjecutarDirecto)
            return operation(Contexto, state);

        EnEjecucion.Value = true;
        try
        {
            return pipeline.Execute(() => operation(Contexto, state));
        }
        finally
        {
            EnEjecucion.Value = false;
        }
    }

    public async Task<TResult> ExecuteAsync<TState, TResult>(TState state,
        Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
        CancellationToken cancellationToken = default)
    {
        if (EjecutarDirecto)
            return await operation(Contexto, state, cancellationToken);

        EnEjecucion.Value = true;
        try
        {
            return await pipeline.ExecuteAsync(
                async token => await operation(Contexto, state, token),
                cancellationToken);
        }
        finally
        {
            EnEjecucion.Value = false;
        }
    }
}
