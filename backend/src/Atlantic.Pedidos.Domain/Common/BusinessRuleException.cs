namespace Atlantic.Pedidos.Domain.Common;

/// <summary>
/// Violación de una regla de negocio. El dominio la lanza; la API la traduce a 422.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>Código estable para que el cliente pueda reaccionar sin parsear el mensaje.</summary>
    public string Code { get; }
}
