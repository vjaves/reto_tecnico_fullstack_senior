using Atlantic.Pedidos.Domain.Common;

namespace Atlantic.Pedidos.Domain.Catalogos;

public sealed record DatosProducto(string Codigo, string Nombre, string? Descripcion, int UnidadMedidaId, bool Activo);

public class Producto
{
    private Producto() { }

    public int Id { get; private set; }
    public int? EmpresaId { get; private set; }
    public int? UnidadMedidaId { get; private set; }
    public string? CodigoProducto { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public bool Estado { get; private set; }
    public bool Eliminado { get; private set; }

    public int UsuarioCreacionId { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public int? UsuarioModificacionId { get; private set; }
    public DateTime? FechaModificacion { get; private set; }

    public UnidadMedida? UnidadMedida { get; private set; }

    public static Producto Crear(int empresaId, DatosProducto datos, int usuarioId, DateTime ahora)
    {
        var producto = new Producto
        {
            EmpresaId = empresaId,
            UsuarioCreacionId = usuarioId,
            FechaCreacion = ahora
        };
        producto.Aplicar(datos);
        return producto;
    }

    public void Actualizar(DatosProducto datos, int usuarioId, DateTime ahora)
    {
        if (Eliminado)
            throw new BusinessRuleException("PRODUCTO_ELIMINADO", "El producto fue eliminado y no se puede modificar.");

        Aplicar(datos);
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    /// <summary>Baja lógica: las líneas de pedidos históricos siguen apuntando al producto.</summary>
    public void Eliminar(int usuarioId, DateTime ahora)
    {
        if (Eliminado)
            throw new BusinessRuleException("PRODUCTO_YA_ELIMINADO", "El producto ya fue eliminado.");

        Eliminado = true;
        Estado = false;
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    private void Aplicar(DatosProducto d)
    {
        if (string.IsNullOrWhiteSpace(d.Codigo))
            throw new BusinessRuleException("PRODUCTO_CODIGO_REQUERIDO", "El código es obligatorio.");
        if (string.IsNullOrWhiteSpace(d.Nombre))
            throw new BusinessRuleException("PRODUCTO_NOMBRE_REQUERIDO", "El nombre es obligatorio.");

        CodigoProducto = NormalizarCodigo(d.Codigo);
        Nombre = d.Nombre.Trim();
        Descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim();
        UnidadMedidaId = d.UnidadMedidaId;
        Estado = d.Activo;
    }
}
