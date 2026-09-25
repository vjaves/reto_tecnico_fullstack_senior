using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Common;

namespace Atlantic.Pedidos.Domain.Pedidos;

public class PedidoDetalle
{
    private PedidoDetalle() { }

    internal PedidoDetalle(int item, LineaPedido linea, int usuarioId, DateTime ahora)
    {
        Item = item;
        FechaCreacion = ahora;
        UsuarioCreacionId = usuarioId;
        Eliminado = false;
        Aplicar(linea);
    }

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public int ProductoId { get; private set; }
    public int Item { get; internal set; }
    public decimal Cantidad { get; private set; }
    public decimal PrecioVenta { get; private set; }
    public decimal Descuento { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal ImporteTotal { get; private set; }
    public bool Eliminado { get; private set; }

    public int? UsuarioCreacionId { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public int? UsuarioModificacionId { get; private set; }
    public DateTime? FechaModificacion { get; private set; }

    public Producto? Producto { get; private set; }

    internal void Modificar(LineaPedido linea, int usuarioId, DateTime ahora)
    {
        Aplicar(linea);
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    internal void Eliminar(int usuarioId, DateTime ahora)
    {
        Eliminado = true;
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    private void Aplicar(LineaPedido linea)
    {
        if (linea.Cantidad <= 0)
            throw new BusinessRuleException("DETALLE_CANTIDAD_INVALIDA", "La cantidad de cada línea debe ser mayor a 0.");
        if (linea.PrecioVenta < 0)
            throw new BusinessRuleException("DETALLE_PRECIO_INVALIDO", "El precio de venta no puede ser negativo.");
        if (linea.Descuento < 0)
            throw new BusinessRuleException("DETALLE_DESCUENTO_INVALIDO", "El descuento no puede ser negativo.");

        var subTotal = Math.Round(linea.Cantidad * linea.PrecioVenta, 2, MidpointRounding.AwayFromZero);
        if (linea.Descuento > subTotal)
            throw new BusinessRuleException("DETALLE_DESCUENTO_EXCEDE", "El descuento de una línea no puede superar su subtotal.");

        ProductoId = linea.ProductoId;
        Cantidad = linea.Cantidad;
        PrecioVenta = linea.PrecioVenta;
        Descuento = linea.Descuento;
        SubTotal = subTotal;
        ImporteTotal = subTotal - linea.Descuento;
    }
}
