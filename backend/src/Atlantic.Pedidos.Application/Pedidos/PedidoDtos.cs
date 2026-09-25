namespace Atlantic.Pedidos.Application.Pedidos;

public sealed record PedidoResumenDto(
    int Id,
    string NumeroPedido,
    int ClienteId,
    string Cliente,
    DateOnly Fecha,
    string Moneda,
    decimal Total,
    string Estado,
    int CantidadLineas);

public sealed record PedidoDetalleDto(
    int Id,
    int Item,
    int ProductoId,
    string? CodigoProducto,
    string Producto,
    decimal Cantidad,
    decimal PrecioVenta,
    decimal Descuento,
    decimal SubTotal,
    decimal ImporteTotal);

public sealed record PedidoDto(
    int Id,
    string NumeroPedido,
    int ClienteId,
    string Cliente,
    int SucursalId,
    string Sucursal,
    int MonedaId,
    string Moneda,
    DateOnly Fecha,
    decimal TotalDescuentos,
    decimal Total,
    string Estado,
    DateTime FechaCreacion,
    DateTime? FechaModificacion,
    string RowVersion,
    IReadOnlyList<PedidoDetalleDto> Detalles);

public sealed record PedidoLineaRequest(int? Id, int ProductoId, decimal Cantidad, decimal PrecioVenta, decimal Descuento = 0);

public abstract record PedidoRequestBase
{
    public string NumeroPedido { get; init; } = string.Empty;
    public int ClienteId { get; init; }
    public int SucursalId { get; init; }
    public int MonedaId { get; init; }
    public DateOnly Fecha { get; init; }
    public IReadOnlyList<PedidoLineaRequest> Detalles { get; init; } = [];
}

public sealed record CrearPedidoRequest : PedidoRequestBase;

public sealed record ActualizarPedidoRequest : PedidoRequestBase
{
    public string Estado { get; init; } = string.Empty;

    /// <summary>Valor de <c>rowVersion</c> leído en el GET: si otro usuario guardó antes, se responde 409.</summary>
    public string? RowVersion { get; init; }
}

public sealed record PedidoFiltro
{
    public string? Buscar { get; init; }
    public string? Estado { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    /// <summary>numero | cliente | fecha | total | estado</summary>
    public string? OrdenarPor { get; init; }
    public bool Descendente { get; init; } = true;
}

public sealed record SiguienteNumeroDto(string NumeroPedido);

/// <summary>Cantidad de pedidos vigentes (no anulados) por estado, para las tarjetas del listado.</summary>
public sealed record PedidoResumenEstadosDto(int Total, IReadOnlyDictionary<string, int> PorEstado);
