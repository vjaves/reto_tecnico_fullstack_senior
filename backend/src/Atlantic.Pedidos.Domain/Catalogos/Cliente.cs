using System.Text.RegularExpressions;
using Atlantic.Pedidos.Domain.Common;

namespace Atlantic.Pedidos.Domain.Catalogos;

public sealed record DatosCliente(
    string NumeroDocumento,
    string? PrimerNombre,
    string? SegundoNombre,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? RazonSocial,
    string? NombreComercial,
    string? Direccion,
    string? Celular,
    bool Activo);

public partial class Cliente
{
    private Cliente() { }

    public int Id { get; private set; }
    public int EmpresaId { get; private set; }
    public int TipoDocumentoId { get; private set; }
    public string NumeroDocumento { get; private set; } = string.Empty;
    public string? ApellidoPaterno { get; private set; }
    public string? ApellidoMaterno { get; private set; }
    public string? PrimerNombre { get; private set; }
    public string? SegundoNombre { get; private set; }
    public string? RazonSocial { get; private set; }
    public string? NombreComercial { get; private set; }
    public string? Direccion { get; private set; }
    public string? Celular { get; private set; }
    public bool? Estado { get; private set; }
    public bool? Eliminado { get; private set; }

    public int? UsuarioCreacionId { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public int? UsuarioModificacionId { get; private set; }
    public DateTime? FechaModificacion { get; private set; }
    public int? UsuarioAnulacionId { get; private set; }
    public DateTime? FechaAnulacion { get; private set; }

    public TipoDocumento? TipoDocumento { get; private set; }

    public bool EstaEliminado => Eliminado == true;

    public static Cliente Crear(int empresaId, TipoDocumento tipo, DatosCliente datos, int usuarioId, DateTime ahora)
    {
        var cliente = new Cliente
        {
            EmpresaId = empresaId,
            Eliminado = false,
            UsuarioCreacionId = usuarioId,
            FechaCreacion = ahora
        };
        cliente.Aplicar(tipo, datos);
        return cliente;
    }

    public void Actualizar(TipoDocumento tipo, DatosCliente datos, int usuarioId, DateTime ahora)
    {
        if (EstaEliminado)
            throw new BusinessRuleException("CLIENTE_ELIMINADO", "El cliente fue eliminado y no se puede modificar.");

        Aplicar(tipo, datos);
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    /// <summary>Baja lógica: los pedidos históricos conservan la referencia al cliente.</summary>
    public void Eliminar(int usuarioId, DateTime ahora)
    {
        if (EstaEliminado)
            throw new BusinessRuleException("CLIENTE_YA_ELIMINADO", "El cliente ya fue eliminado.");

        Eliminado = true;
        Estado = false;
        UsuarioAnulacionId = usuarioId;
        FechaAnulacion = ahora;
    }

    /// <summary>
    /// Regla del documento según su tipo. Devuelve el mensaje de error o null.
    /// Es pública para que la capa de aplicación la muestre como error del campo antes de llegar aquí.
    /// </summary>
    public static string? ValidarDocumento(string? abreviaturaTipo, string numero)
    {
        numero = numero.Trim();
        return abreviaturaTipo?.ToUpperInvariant() switch
        {
            "DNI" when !SoloDigitos().IsMatch(numero) || numero.Length != 8 => "El DNI debe tener 8 dígitos.",
            "RUC" when !SoloDigitos().IsMatch(numero) || numero.Length != 11 => "El RUC debe tener 11 dígitos.",
            "RUC" when !numero.StartsWith("10") && !numero.StartsWith("15") && !numero.StartsWith("17") && !numero.StartsWith("20")
                => "El RUC debe empezar con 10, 15, 17 o 20.",
            _ when numero.Length is < 4 or > 20 => "El documento debe tener entre 4 y 20 caracteres.",
            _ => null
        };
    }

    /// <summary>RUC = contribuyente con razón social; cualquier otro documento = persona con nombres.</summary>
    public static bool EsTipoEmpresa(string? abreviaturaTipo) =>
        string.Equals(abreviaturaTipo, "RUC", StringComparison.OrdinalIgnoreCase);

    private void Aplicar(TipoDocumento tipo, DatosCliente d)
    {
        if (ValidarDocumento(tipo.Abreviatura, d.NumeroDocumento) is { } error)
            throw new BusinessRuleException("CLIENTE_DOCUMENTO_INVALIDO", error);

        TipoDocumentoId = tipo.Id;
        NumeroDocumento = d.NumeroDocumento.Trim();
        NombreComercial = Limpio(d.NombreComercial);
        Direccion = Limpio(d.Direccion);
        Celular = Limpio(d.Celular);
        Estado = d.Activo;

        // Se guarda sólo el juego de nombres que corresponde al tipo: el nombre visible se arma como
        // "RazonSocial ?? nombres", y un resto del otro juego lo contaminaría.
        if (EsTipoEmpresa(tipo.Abreviatura))
        {
            RazonSocial = Limpio(d.RazonSocial)
                          ?? throw new BusinessRuleException("CLIENTE_RAZON_SOCIAL", "La razón social es obligatoria para un RUC.");
            PrimerNombre = SegundoNombre = ApellidoPaterno = ApellidoMaterno = null;
        }
        else
        {
            PrimerNombre = Limpio(d.PrimerNombre)
                           ?? throw new BusinessRuleException("CLIENTE_NOMBRE", "El nombre es obligatorio.");
            ApellidoPaterno = Limpio(d.ApellidoPaterno)
                              ?? throw new BusinessRuleException("CLIENTE_APELLIDO", "El apellido paterno es obligatorio.");
            SegundoNombre = Limpio(d.SegundoNombre);
            ApellidoMaterno = Limpio(d.ApellidoMaterno);
            RazonSocial = null;
        }
    }

    private static string? Limpio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex SoloDigitos();
}
