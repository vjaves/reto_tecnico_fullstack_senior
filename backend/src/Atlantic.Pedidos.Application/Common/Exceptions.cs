namespace Atlantic.Pedidos.Application.Common;

// Excepciones de aplicación. Cada una corresponde a un status HTTP en GlobalExceptionHandler;
// los servicios no conocen HTTP, sólo el significado del error.

/// <summary>404: el recurso no existe o no pertenece a la empresa del usuario.</summary>
public sealed class NotFoundException(string recurso, object clave)
    : Exception($"{recurso} '{clave}' no existe.");

/// <summary>409: choca con el estado actual (número duplicado, edición concurrente).</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>401: credenciales inválidas o cuenta bloqueada.</summary>
public sealed class AuthenticationFailedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>403: autenticado, pero sin alcance sobre el recurso.</summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);
