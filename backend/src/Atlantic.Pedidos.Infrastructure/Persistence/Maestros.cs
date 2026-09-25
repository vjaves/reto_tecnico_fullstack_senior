using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Clientes;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Productos;
using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace Atlantic.Pedidos.Infrastructure.Persistence;

/// <summary>Estados en los que un pedido todavía "usa" a su cliente y a sus productos.</summary>
internal static class EstadosEnCurso
{
    public static readonly EstadoPedido[] Valores = [EstadoPedido.Registrado, EstadoPedido.Confirmado, EstadoPedido.Despachado];
}

internal sealed class ClienteRepository(AppDbContext db) : IClienteRepository
{
    public Task<Cliente?> ObtenerAsync(int id, int empresaId, CancellationToken ct) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId && c.Eliminado != true, ct);

    public Task<bool> ExisteDocumentoAsync(int empresaId, int tipoDocumentoId, string numero, int? excluirId, CancellationToken ct) =>
        db.Clientes.AnyAsync(c => c.EmpresaId == empresaId
                                  && c.TipoDocumentoId == tipoDocumentoId
                                  && c.NumeroDocumento == numero
                                  && c.Eliminado != true
                                  && (excluirId == null || c.Id != excluirId), ct);

    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);
}

internal sealed class ProductoRepository(AppDbContext db) : IProductoRepository
{
    public Task<Producto?> ObtenerAsync(int id, int empresaId, CancellationToken ct) =>
        db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId && !p.Eliminado, ct);

    public Task<bool> ExisteCodigoAsync(int empresaId, string codigo, int? excluirId, CancellationToken ct) =>
        db.Productos.AnyAsync(p => p.EmpresaId == empresaId
                                   && p.CodigoProducto == codigo
                                   && !p.Eliminado
                                   && (excluirId == null || p.Id != excluirId), ct);

    public void Agregar(Producto producto) => db.Productos.Add(producto);
}

internal sealed class ClienteQueries(AppDbContext db) : IClienteQueries
{
    public async Task<PagedResult<ClienteResumenDto>> ListarAsync(int empresaId, MaestroFiltro filtro, CancellationToken ct)
    {
        var query =
            from c in db.Clientes.AsNoTracking()
            where c.EmpresaId == empresaId && c.Eliminado != true
            select new
            {
                c.Id,
                TipoDocumento = c.TipoDocumento!.Abreviatura ?? c.TipoDocumento.Nombre,
                c.NumeroDocumento,
                Nombre = (c.RazonSocial ?? (c.PrimerNombre ?? "") + " " + (c.ApellidoPaterno ?? "") + " " +
                    (c.ApellidoMaterno ?? "")).Trim(),
                c.Celular,
                Activo = c.Estado != false,
                c.NombreComercial
            };

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var texto = filtro.Buscar.Trim();
            query = query.Where(x => x.Nombre.Contains(texto) || x.NumeroDocumento.Contains(texto)
                                                               || (x.NombreComercial != null && x.NombreComercial.Contains(texto)));
        }

        if (filtro.SoloActivos is { } activos)
            query = query.Where(x => x.Activo == activos);

        var total = await query.CountAsync(ct);
        var filas = await query
            .OrderBy(x => x.Nombre)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(x => new
            {
                x.Id,
                x.TipoDocumento,
                x.NumeroDocumento,
                x.Nombre,
                x.Celular,
                x.Activo,
                EnCurso = db.Pedidos.Count(p => p.ClienteId == x.Id && EstadosEnCurso.Valores.Contains(p.Estado))
            })
            .ToListAsync(ct);

        var items = filas
            .Select(f => new ClienteResumenDto(f.Id, f.TipoDocumento, f.NumeroDocumento, f.Nombre, f.Celular, f.Activo, f.EnCurso))
            .ToList();
        return new PagedResult<ClienteResumenDto>(items, filtro.Page, filtro.PageSize, total);
    }

    public Task<ClienteDetalleDto?> ObtenerAsync(int id, int empresaId, CancellationToken ct) =>
        db.Clientes.AsNoTracking()
            .Where(c => c.Id == id && c.EmpresaId == empresaId && c.Eliminado != true)
            .Select(c => new ClienteDetalleDto(c.Id, c.TipoDocumentoId, c.TipoDocumento!.Abreviatura ?? c.TipoDocumento.Nombre,
                c.NumeroDocumento, c.PrimerNombre, c.SegundoNombre, c.ApellidoPaterno, c.ApellidoMaterno, c.RazonSocial,
                c.NombreComercial, c.Direccion, c.Celular, c.Estado != false, c.FechaCreacion, c.FechaModificacion))
            .FirstOrDefaultAsync(ct);

    public Task<int> PedidosEnCursoAsync(int clienteId, CancellationToken ct) =>
        db.Pedidos.CountAsync(p => p.ClienteId == clienteId && EstadosEnCurso.Valores.Contains(p.Estado), ct);
}

internal sealed class ProductoQueries(AppDbContext db) : IProductoQueries
{
    public async Task<PagedResult<ProductoResumenDto>> ListarAsync(int empresaId, MaestroFiltro filtro, CancellationToken ct)
    {
        var query = db.Productos.AsNoTracking().Where(p => p.EmpresaId == empresaId && !p.Eliminado);

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var texto = filtro.Buscar.Trim();
            query = query.Where(p => p.Nombre.Contains(texto) || (p.CodigoProducto != null && p.CodigoProducto.Contains(texto)));
        }

        if (filtro.SoloActivos is { } activos)
            query = query.Where(p => p.Estado == activos);

        var total = await query.CountAsync(ct);
        var filas = await query
            .OrderBy(p => p.CodigoProducto).ThenBy(p => p.Nombre)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(p => new
            {
                p.Id,
                p.CodigoProducto,
                p.Nombre,
                UnidadMedida = p.UnidadMedida != null ? p.UnidadMedida.Nombre : null,
                p.Estado,
                // Las líneas eliminadas ya las excluye el filtro global de PedidoDetalle; los pedidos
                // anulados, el de Pedido.
                EnCurso = db.PedidoDetalles
                    .Where(d => d.ProductoId == p.Id)
                    .Join(db.Pedidos.Where(pe => EstadosEnCurso.Valores.Contains(pe.Estado)), d => d.PedidoId, pe => pe.Id, (d, pe) => pe.Id)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(ct);

        var items = filas
            .Select(f => new ProductoResumenDto(f.Id, f.CodigoProducto, f.Nombre, f.UnidadMedida, f.Estado, f.EnCurso))
            .ToList();
        return new PagedResult<ProductoResumenDto>(items, filtro.Page, filtro.PageSize, total);
    }

    public Task<ProductoDetalleDto?> ObtenerAsync(int id, int empresaId, CancellationToken ct) =>
        db.Productos.AsNoTracking()
            .Where(p => p.Id == id && p.EmpresaId == empresaId && !p.Eliminado)
            .Select(p => new ProductoDetalleDto(p.Id, p.CodigoProducto, p.Nombre, p.Descripcion, p.UnidadMedidaId,
                p.UnidadMedida != null ? p.UnidadMedida.Nombre : null, p.Estado, p.FechaCreacion, p.FechaModificacion))
            .FirstOrDefaultAsync(ct);

    public Task<int> PedidosEnCursoAsync(int productoId, CancellationToken ct) =>
        db.PedidoDetalles
            .Where(d => d.ProductoId == productoId)
            .Join(db.Pedidos.Where(pe => EstadosEnCurso.Valores.Contains(pe.Estado)), d => d.PedidoId, pe => pe.Id, (d, pe) => pe.Id)
            .Distinct()
            .CountAsync(ct);
}
