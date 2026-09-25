namespace Atlantic.Pedidos.Domain.Catalogos;

// Catálogos de sólo lectura: se mapean a las tablas del modelo referencial y alimentan los combos.
// Cliente y Producto tienen su propio archivo porque sí se mantienen desde la aplicación.

public class Moneda
{
    private Moneda() { }

    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string? Abreviatura { get; private set; }
    public bool? Estado { get; private set; }
}

public class Sucursal
{
    private Sucursal() { }

    public int Id { get; private set; }
    public int EmpresaId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public bool? Estado { get; private set; }
}

public class TipoDocumento
{
    private TipoDocumento() { }

    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string? Abreviatura { get; private set; }
    public bool? Estado { get; private set; }
}

public class UnidadMedida
{
    private UnidadMedida() { }

    public int Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? Abreviatura { get; private set; }
    public bool? Estado { get; private set; }
}
