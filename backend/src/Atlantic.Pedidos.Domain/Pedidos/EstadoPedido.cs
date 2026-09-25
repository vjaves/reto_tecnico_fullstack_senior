namespace Atlantic.Pedidos.Domain.Pedidos;

/// <summary>
/// Ciclo de vida del pedido. El orden numérico es el orden del flujo: un pedido sólo avanza.
/// Se persiste como texto (columna Pedido.Estado), así que renombrar un valor es un cambio de contrato.
/// </summary>
public enum EstadoPedido
{
    Registrado = 1,
    Confirmado = 2,
    Despachado = 3,
    Entregado = 4,
    Anulado = 9
}
