using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Catalogos;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Pedidos;
using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace Atlantic.Pedidos.Infrastructure.Persistence;

internal sealed class PedidoQueries(AppDbContext db) : IPedidoQueries
{
    public async Task<PagedResult<PedidoResumenDto>> ListarAsync(int empresaId, PedidoFiltro filtro, CancellationToken ct)
    {
        var query =
            from p in db.Pedidos.AsNoTracking()
            where p.Sucursal!.EmpresaId == empresaId
            select new
            {
                Pedido = p,
                // Persona natural: nombres; empresa: razón social. Mismo criterio que el combo de clientes.
                ClienteNombre = (p.Cliente!.RazonSocial ??
                                 (p.Cliente.PrimerNombre ?? "") + " " + (p.Cliente.ApellidoPaterno ?? "") + " " +
                                 (p.Cliente.ApellidoMaterno ?? "")).Trim(),
                p.Cliente.NumeroDocumento
            };

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var texto = filtro.Buscar.Trim();
            query = query.Where(x => x.Pedido.NumeroPedido.Contains(texto)
                                     || x.ClienteNombre.Contains(texto)
                                     || x.NumeroDocumento.Contains(texto));
        }

        if (filtro.Estado is not null)
        {
            var estado = Enum.Parse<EstadoPedido>(filtro.Estado, ignoreCase: true);
            query = query.Where(x => x.Pedido.Estado == estado);
        }

        if (filtro.Desde is { } desde)
        {
            var d = desde.ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.Pedido.FechaPedido >= d);
        }

        if (filtro.Hasta is { } hasta)
        {
            var h = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.Pedido.FechaPedido < h);
        }

        var total = await query.CountAsync(ct);

        query = (filtro.OrdenarPor?.ToLowerInvariant(), filtro.Descendente) switch
        {
            ("numero", false) => query.OrderBy(x => x.Pedido.NumeroPedido),
            ("numero", true) => query.OrderByDescending(x => x.Pedido.NumeroPedido),
            ("cliente", false) => query.OrderBy(x => x.ClienteNombre),
            ("cliente", true) => query.OrderByDescending(x => x.ClienteNombre),
            ("total", false) => query.OrderBy(x => x.Pedido.TotalPedido),
            ("total", true) => query.OrderByDescending(x => x.Pedido.TotalPedido),
            ("estado", false) => query.OrderBy(x => x.Pedido.Estado),
            ("estado", true) => query.OrderByDescending(x => x.Pedido.Estado),
            (_, false) => query.OrderBy(x => x.Pedido.FechaPedido).ThenBy(x => x.Pedido.Id),
            _ => query.OrderByDescending(x => x.Pedido.FechaPedido).ThenByDescending(x => x.Pedido.Id)
        };

        var filas = await query
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(x => new
            {
                x.Pedido.Id,
                x.Pedido.NumeroPedido,
                x.Pedido.ClienteId,
                x.ClienteNombre,
                x.Pedido.FechaPedido,
                Moneda = x.Pedido.Moneda!.Abreviatura ?? x.Pedido.Moneda.Nombre,
                x.Pedido.TotalPedido,
                x.Pedido.Estado,
                Lineas = x.Pedido.Detalles.Count()
            })
            .ToListAsync(ct);

        var items = filas
            .Select(f => new PedidoResumenDto(f.Id, f.NumeroPedido, f.ClienteId, f.ClienteNombre,
                DateOnly.FromDateTime(f.FechaPedido), f.Moneda, f.TotalPedido, f.Estado.ToString(), f.Lineas))
            .ToList();

        return new PagedResult<PedidoResumenDto>(items, filtro.Page, filtro.PageSize, total);
    }

    public async Task<PedidoDto?> ObtenerAsync(int id, int empresaId, CancellationToken ct)
    {
        var p = await db.Pedidos.AsNoTracking()
            .Where(x => x.Id == id && x.Sucursal!.EmpresaId == empresaId)
            .Select(x => new
            {
                x.Id,
                x.NumeroPedido,
                x.ClienteId,
                Cliente = (x.Cliente!.RazonSocial ??
                           (x.Cliente.PrimerNombre ?? "") + " " + (x.Cliente.ApellidoPaterno ?? "") + " " +
                           (x.Cliente.ApellidoMaterno ?? "")).Trim(),
                x.SucursalId,
                Sucursal = x.Sucursal!.Nombre,
                x.MonedaId,
                Moneda = x.Moneda!.Abreviatura ?? x.Moneda.Nombre,
                x.FechaPedido,
                x.TotalDescuentos,
                x.TotalPedido,
                x.Estado,
                x.FechaCreacion,
                x.FechaModificacion,
                x.RowVersion,
                Detalles = x.Detalles
                    .OrderBy(d => d.Item)
                    .Select(d => new PedidoDetalleDto(d.Id, d.Item, d.ProductoId, d.Producto!.CodigoProducto,
                        d.Producto.Nombre, d.Cantidad, d.PrecioVenta, d.Descuento, d.SubTotal, d.ImporteTotal))
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        return p is null
            ? null
            : new PedidoDto(p.Id, p.NumeroPedido, p.ClienteId, p.Cliente, p.SucursalId, p.Sucursal, p.MonedaId,
                p.Moneda, DateOnly.FromDateTime(p.FechaPedido), p.TotalDescuentos, p.TotalPedido, p.Estado.ToString(),
                p.FechaCreacion, p.FechaModificacion, Convert.ToBase64String(p.RowVersion), p.Detalles);
    }

    public async Task<string> SiguienteNumeroAsync(CancellationToken ct)
    {
        // Incluye anulados: su número sigue reservado por el índice único.
        var ultimo = await db.Database
            .SqlQueryRaw<int>(
                "SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(NumeroPedido, 5, 16) AS int)), 0) AS [Value] " +
                "FROM dbo.Pedido WHERE NumeroPedido LIKE 'PED-%'")
            .SingleAsync(ct);

        return $"PED-{ultimo + 1:D3}";
    }

    public async Task<PedidoResumenEstadosDto> ResumenAsync(int empresaId, CancellationToken ct)
    {
        var conteos = await db.Pedidos.AsNoTracking()
            .Where(p => p.Sucursal!.EmpresaId == empresaId)
            .GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);

        // Todos los estados vigentes aparecen, aunque tengan 0: el front pinta una tarjeta por estado.
        var porEstado = Enum.GetValues<EstadoPedido>()
            .Where(e => e != EstadoPedido.Anulado)
            .ToDictionary(e => e.ToString(), e => conteos.FirstOrDefault(c => c.Estado == e)?.Cantidad ?? 0);

        return new PedidoResumenEstadosDto(porEstado.Values.Sum(), porEstado);
    }
}

internal sealed class CatalogoQueries(AppDbContext db) : ICatalogoQueries
{
    private IQueryable<Cliente> ClientesActivos(int empresaId) =>
        db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Estado != false && c.Eliminado != true);

    public async Task<IReadOnlyList<ClienteDto>> ClientesAsync(int empresaId, CancellationToken ct)
    {
        var filas = await ClientesActivos(empresaId)
            .Select(c => new
            {
                c.Id,
                c.NumeroDocumento,
                Nombre = (c.RazonSocial ?? (c.PrimerNombre ?? "") + " " + (c.ApellidoPaterno ?? "") + " " +
                    (c.ApellidoMaterno ?? "")).Trim()
            })
            .OrderBy(c => c.Nombre)
            .ToListAsync(ct);

        return filas.Select(c => new ClienteDto(c.Id, c.NumeroDocumento, c.Nombre)).ToList();
    }

    public async Task<IReadOnlyList<ProductoDto>> ProductosAsync(int empresaId, CancellationToken ct) =>
        await db.Productos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Estado && !p.Eliminado)
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoDto(p.Id, p.CodigoProducto, p.Nombre))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ItemCatalogoDto>> MonedasAsync(CancellationToken ct) =>
        await db.Monedas.AsNoTracking()
            .Where(m => m.Estado != false)
            .OrderBy(m => m.Id)
            .Select(m => new ItemCatalogoDto(m.Id, m.Nombre, m.Abreviatura))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ItemCatalogoDto>> SucursalesAsync(int empresaId, CancellationToken ct) =>
        await db.Sucursales.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.Estado != false)
            .OrderBy(s => s.Id)
            .Select(s => new ItemCatalogoDto(s.Id, s.Nombre, null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ItemCatalogoDto>> TiposDocumentoAsync(CancellationToken ct) =>
        await db.TiposDocumento.AsNoTracking()
            .Where(t => t.Estado != false)
            .OrderBy(t => t.Id)
            .Select(t => new ItemCatalogoDto(t.Id, t.Nombre, t.Abreviatura))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ItemCatalogoDto>> UnidadesMedidaAsync(CancellationToken ct) =>
        await db.UnidadesMedida.AsNoTracking()
            .Where(u => u.Estado != false)
            .OrderBy(u => u.Nombre)
            .Select(u => new ItemCatalogoDto(u.Id, u.Nombre, u.Abreviatura))
            .ToListAsync(ct);

    public Task<TipoDocumento?> TipoDocumentoAsync(int id, CancellationToken ct) =>
        db.TiposDocumento.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.Estado != false, ct);

    public Task<bool> UnidadMedidaValidaAsync(int id, CancellationToken ct) =>
        db.UnidadesMedida.AnyAsync(u => u.Id == id && u.Estado != false, ct);

    public Task<bool> ClienteValidoAsync(int empresaId, int clienteId, CancellationToken ct) =>
        ClientesActivos(empresaId).AnyAsync(c => c.Id == clienteId, ct);

    public Task<bool> SucursalValidaAsync(int empresaId, int sucursalId, CancellationToken ct) =>
        db.Sucursales.AnyAsync(s => s.Id == sucursalId && s.EmpresaId == empresaId && s.Estado != false, ct);

    public Task<bool> MonedaValidaAsync(int monedaId, CancellationToken ct) =>
        db.Monedas.AnyAsync(m => m.Id == monedaId && m.Estado != false, ct);

    public async Task<IReadOnlySet<int>> ProductosValidosAsync(int empresaId, IReadOnlyCollection<int> productoIds, CancellationToken ct)
    {
        var validos = await db.Productos.AsNoTracking()
            .Where(p => productoIds.Contains(p.Id) && p.EmpresaId == empresaId && p.Estado && !p.Eliminado)
            .Select(p => p.Id)
            .ToListAsync(ct);
        return validos.ToHashSet();
    }
}
