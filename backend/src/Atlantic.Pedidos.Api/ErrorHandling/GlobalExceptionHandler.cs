using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Infrastructure.Resilience;
using FluentValidation;
using Polly.CircuitBreaker;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Atlantic.Pedidos.Api.ErrorHandling;

/// <summary>
/// Punto único donde una excepción se convierte en respuesta HTTP (RFC 7807).
/// Los controladores no llevan try/catch; los servicios sólo expresan qué falló.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title, code) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Datos inválidos", "VALIDACION"),
            BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio", e.Code),
            NotFoundException => (StatusCodes.Status404NotFound, "No encontrado", "NO_ENCONTRADO"),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflicto", e.Code),
            AuthenticationFailedException e => (StatusCodes.Status401Unauthorized, "No autenticado", e.Code),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Acceso denegado", "ACCESO_DENEGADO"),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested =>
                (499, "Petición cancelada", "CANCELADO"),
            BrokenCircuitException => (StatusCodes.Status503ServiceUnavailable, "Servicio no disponible", "CIRCUITO_ABIERTO"),
            _ when SqlResilience.EsFalloDeInfraestructura(exception) =>
                (StatusCodes.Status503ServiceUnavailable, "Servicio no disponible", "BD_NO_DISPONIBLE"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "ERROR_INTERNO")
        };

        if (status == StatusCodes.Status503ServiceUnavailable)
            // Con el circuito abierto cada petición falla igual: sin stack trace para no inundar el log.
            logger.LogWarning("503 {Code} en {Metodo} {Ruta}: {Mensaje}",
                code, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        else if (status >= 500)
            logger.LogError(exception, "Error no controlado en {Metodo} {Ruta}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogInformation("{Status} {Code} en {Metodo} {Ruta}: {Mensaje}",
                status, code, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // En un 5xx no se expone el mensaje interno: puede revelar SQL, rutas o datos.
            Detail = status switch
            {
                StatusCodes.Status503ServiceUnavailable =>
                    "La base de datos no está disponible en este momento. Intente nuevamente en unos segundos.",
                >= 500 => "Ocurrió un error inesperado. Si persiste, informe el traceId.",
                _ => exception.Message
            },
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = code;

        if (exception is ValidationException validation)
        {
            problem.Detail = "Revise los campos marcados.";
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => ACamelCase(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        }

        if (status == StatusCodes.Status503ServiceUnavailable)
        {
            var reintentar = exception is BrokenCircuitException { RetryAfter: { } espera } ? espera : TimeSpan.FromSeconds(5);
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(reintentar.TotalSeconds)).ToString();
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    /// <summary>"Detalles[0].Cantidad" → "detalles[0].cantidad", para que el front lo asocie al campo.</summary>
    private static string ACamelCase(string propertyName) =>
        string.Join('.', propertyName.Split('.')
            .Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
