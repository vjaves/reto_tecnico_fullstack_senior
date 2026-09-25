using Serilog.Context;

namespace Atlantic.Pedidos.Api.Middleware;

/// <summary>
/// Correlation id: reutiliza el X-Correlation-ID entrante o genera uno, lo devuelve en la respuesta
/// y lo agrega a todas las líneas de log de la petición.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(Header, out var valor)
                            && !string.IsNullOrWhiteSpace(valor) && valor.ToString().Length <= 64
            ? valor.ToString()
            : context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>Headers defensivos básicos para una API que sólo devuelve JSON.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            if (!context.Request.Path.StartsWithSegments("/swagger"))
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
