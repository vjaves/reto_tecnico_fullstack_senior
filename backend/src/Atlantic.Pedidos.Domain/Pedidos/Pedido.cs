using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Common;

namespace Atlantic.Pedidos.Domain.Pedidos;

/// <summary>
/// Raíz del agregado. Toda modificación de líneas, totales y estado pasa por aquí,
/// así las reglas de negocio no dependen de que el llamador se acuerde de validarlas.
/// </summary>
public class Pedido
{
    private readonly List<PedidoDetalle> _detalles = [];

    private Pedido() { }

    public int Id { get; private set; }
    public int SucursalId { get; private set; }
    public int ClienteId { get; private set; }
    public int MonedaId { get; private set; }
    public string NumeroPedido { get; private set; } = string.Empty;
    public DateTime FechaPedido { get; private set; }
    public decimal TotalDescuentos { get; private set; }
    public decimal TotalPedido { get; private set; }
    public EstadoPedido Estado { get; private set; }

    public int UsuarioCreacionId { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public int? UsuarioModificacionId { get; private set; }
    public DateTime? FechaModificacion { get; private set; }
    public int? UsuarioAnuladoId { get; private set; }
    public DateTime? FechaAnulado { get; private set; }

    /// <summary>Token de concurrencia optimista (rowversion).</summary>
    public byte[] RowVersion { get; private set; } = [];

    public Cliente? Cliente { get; private set; }
    public Moneda? Moneda { get; private set; }
    public Sucursal? Sucursal { get; private set; }

    /// <summary>Sólo líneas vigentes; las eliminadas quedan en BD con Eliminado = 1.</summary>
    public IReadOnlyCollection<PedidoDetalle> Detalles => _detalles.Where(d => !d.Eliminado).ToList();

    public bool EstaAnulado => FechaAnulado.HasValue;

    public static Pedido Crear(
        string numeroPedido,
        int sucursalId,
        int clienteId,
        int monedaId,
        DateTime fechaPedido,
        IReadOnlyCollection<LineaPedido> lineas,
        int usuarioId,
        DateTime ahora)
    {
        var pedido = new Pedido
        {
            Estado = EstadoPedido.Registrado,
            UsuarioCreacionId = usuarioId,
            FechaCreacion = ahora
        };
        pedido.AsignarCabecera(numeroPedido, sucursalId, clienteId, monedaId, fechaPedido);
        pedido.SincronizarLineas(lineas, usuarioId, ahora);
        return pedido;
    }

    public void Actualizar(
        string numeroPedido,
        int sucursalId,
        int clienteId,
        int monedaId,
        DateTime fechaPedido,
        EstadoPedido nuevoEstado,
        IReadOnlyCollection<LineaPedido> lineas,
        int usuarioId,
        DateTime ahora)
    {
        AsegurarEditable();
        CambiarEstado(nuevoEstado);
        AsignarCabecera(numeroPedido, sucursalId, clienteId, monedaId, fechaPedido);
        SincronizarLineas(lineas, usuarioId, ahora);
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    /// <summary>Eliminación lógica: el registro y su número quedan en BD.</summary>
    public void Anular(int usuarioId, DateTime ahora)
    {
        if (EstaAnulado)
            throw new BusinessRuleException("PEDIDO_YA_ANULADO", "El pedido ya se encuentra anulado.");
        if (Estado == EstadoPedido.Entregado)
            throw new BusinessRuleException("PEDIDO_ENTREGADO", "Un pedido entregado no se puede eliminar.");

        Estado = EstadoPedido.Anulado;
        UsuarioAnuladoId = usuarioId;
        FechaAnulado = ahora;
    }

    private void AsegurarEditable()
    {
        if (EstaAnulado)
            throw new BusinessRuleException("PEDIDO_ANULADO", "Un pedido anulado no se puede modificar.");
        if (Estado == EstadoPedido.Entregado)
            throw new BusinessRuleException("PEDIDO_ENTREGADO", "Un pedido entregado no se puede modificar.");
    }

    private void CambiarEstado(EstadoPedido nuevoEstado)
    {
        if (nuevoEstado == EstadoPedido.Anulado)
            throw new BusinessRuleException("ESTADO_ANULADO_NO_PERMITIDO", "Para anular un pedido use la opción Eliminar.");
        if (nuevoEstado < Estado)
            throw new BusinessRuleException("ESTADO_RETROCESO",
                $"El pedido no puede volver de '{Estado}' a '{nuevoEstado}'.");

        Estado = nuevoEstado;
    }

    private void AsignarCabecera(string numeroPedido, int sucursalId, int clienteId, int monedaId, DateTime fechaPedido)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
            throw new BusinessRuleException("PEDIDO_NUMERO_REQUERIDO", "El número de pedido es obligatorio.");

        NumeroPedido = NormalizarNumero(numeroPedido);
        SucursalId = sucursalId;
        ClienteId = clienteId;
        MonedaId = monedaId;
        FechaPedido = fechaPedido.Date;
    }

    /// <summary>
    /// Actualiza las líneas existentes por Id, agrega las nuevas y da de baja lógica a las que ya no vienen.
    /// </summary>
    private void SincronizarLineas(IReadOnlyCollection<LineaPedido> lineas, int usuarioId, DateTime ahora)
    {
        if (lineas.Count == 0)
            throw new BusinessRuleException("PEDIDO_SIN_LINEAS", "El pedido debe tener al menos una línea.");

        var vigentes = _detalles.Where(d => !d.Eliminado).ToList();
        // Las líneas aún no persistidas tienen Id 0: no se pueden referenciar, sólo reemplazar.
        var porId = vigentes.Where(d => d.Id != 0).ToDictionary(d => d.Id);
        var idsRecibidos = lineas.Where(l => l.Id.HasValue).Select(l => l.Id!.Value).ToHashSet();

        foreach (var idDesconocido in idsRecibidos.Where(id => !porId.ContainsKey(id)))
            throw new BusinessRuleException("DETALLE_NO_PERTENECE", $"La línea {idDesconocido} no pertenece a este pedido.");

        foreach (var detalle in vigentes.Where(d => d.Id == 0))
            _detalles.Remove(detalle); // nunca llegó a BD: no hay nada que dar de baja

        foreach (var detalle in porId.Values.Where(d => !idsRecibidos.Contains(d.Id)))
            detalle.Eliminar(usuarioId, ahora);

        var item = 0;
        foreach (var linea in lineas)
        {
            item++;
            if (linea.Id is { } id)
            {
                var detalle = porId[id];
                detalle.Modificar(linea, usuarioId, ahora);
                detalle.Item = item;
            }
            else
            {
                _detalles.Add(new PedidoDetalle(item, linea, usuarioId, ahora));
            }
        }

        RecalcularTotales();
    }

    private void RecalcularTotales()
    {
        var vigentes = _detalles.Where(d => !d.Eliminado).ToList();
        TotalDescuentos = vigentes.Sum(d => d.Descuento);
        TotalPedido = vigentes.Sum(d => d.ImporteTotal);

        if (TotalPedido <= 0)
            throw new BusinessRuleException("PEDIDO_TOTAL_INVALIDO", "El total del pedido debe ser mayor a 0.");
    }

    public static string NormalizarNumero(string numeroPedido) => numeroPedido.Trim().ToUpperInvariant();
}
