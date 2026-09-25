namespace Atlantic.Pedidos.Domain.Pedidos;

/// <summary>
/// Datos de una línea tal como llegan del caso de uso. <see cref="Id"/> nulo = línea nueva.
/// </summary>
public sealed record LineaPedido(int? Id, int ProductoId, decimal Cantidad, decimal PrecioVenta, decimal Descuento);
