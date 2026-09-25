using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Infrastructure.Persistence;
using Atlantic.Pedidos.Infrastructure.Resilience;
using Atlantic.Pedidos.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace Atlantic.Pedidos.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "miConexion";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
                               ?? throw new InvalidOperationException(
                                   $"Falta la cadena de conexión '{ConnectionStringName}'.");

        // Resiliencia: un único pipeline de Polly (reintento + circuit breaker + timeout) compartido por
        // toda la aplicación, para que el estado del circuito refleje la salud real de SQL Server.
        services.AddOptions<SqlResilienceOptions>()
            .Bind(configuration.GetSection(SqlResilienceOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<CircuitBreakerStateProvider>();
        services.AddResiliencePipeline(SqlResilience.Pipeline, (builder, context) =>
            SqlResilience.Configurar(builder,
                context.ServiceProvider.GetRequiredService<IOptions<SqlResilienceOptions>>().Value,
                context.ServiceProvider.GetRequiredService<CircuitBreakerStateProvider>()));

        services.AddDbContext<AppDbContext>((sp, options) => options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            sql.CommandTimeout(30);
            // Reemplaza a EnableRetryOnFailure: EF ejecuta cada consulta, SaveChanges y migración a través
            // del pipeline de Polly.
            var pipeline = sp.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(SqlResilience.Pipeline);
            sql.ExecutionStrategy(deps => new PollyExecutionStrategy(deps, pipeline));
        }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IUsuarioQueries, UsuarioQueries>();
        services.AddMemoryCache();
        services.AddScoped<IEstadoSesionProvider, EstadoSesionProvider>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IPedidoQueries, PedidoQueries>();
        services.AddScoped<ICatalogoQueries, CatalogoQueries>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IClienteQueries, ClienteQueries>();
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IProductoQueries, ProductoQueries>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }

    /// <summary>
    /// Migraciones automáticas al iniciar. Idempotente: EF sólo aplica las pendientes.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migraciones");

        var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendientes.Count == 0)
        {
            logger.LogInformation("Base de datos al día, sin migraciones pendientes");
            return;
        }

        logger.LogInformation("Aplicando {Cantidad} migración(es): {Migraciones}", pendientes.Count, pendientes);
        await db.Database.MigrateAsync();
        logger.LogInformation("Migraciones aplicadas");
    }
}
