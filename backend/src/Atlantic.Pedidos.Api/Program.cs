using System.Text.Json.Serialization;
using Atlantic.Pedidos.Api.Admin;
using Atlantic.Pedidos.Api.ErrorHandling;
using Atlantic.Pedidos.Api.Middleware;
using Atlantic.Pedidos.Api.OpenApi;
using Atlantic.Pedidos.Api.Security;
using Atlantic.Pedidos.Application;
using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Auth;
using Atlantic.Pedidos.Infrastructure;
using Atlantic.Pedidos.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddJwtSecurity(builder.Configuration);

    builder.Services.Configure<LockoutOptions>(builder.Configuration.GetSection(LockoutOptions.Section));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services
        .AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier));
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(CorrelationIdMiddleware.Header)));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("sqlserver")
        .AddCheck<CircuitoSqlHealthCheck>("circuito-sql");
    builder.Services.AddScoped<EstadoSistemaService>();
    builder.Services.AddSwaggerConJwt();

    var app = builder.Build();

    if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
        await app.ApplyMigrationsAsync();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("UsuarioId", http.User.FindFirst("sub")?.Value ?? "anonimo");
        diag.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString() ?? "");
    });
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.DocumentTitle = "Atlantic Pedidos API");
    }
    else
    {
        app.UseHsts();
    }

    // HTTP → HTTPS en todo entorno con un puerto HTTPS (perfil "https" en desarrollo). El token viaja
    // en un header: por HTTP plano quedaría expuesto a cualquiera que observe la red.
    app.UseHttpsRedirection();

    app.UseRouting();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La API no pudo iniciar");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Expuesto para pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
