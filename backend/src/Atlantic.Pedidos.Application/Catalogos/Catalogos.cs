using Atlantic.Pedidos.Application.Abstractions;

namespace Atlantic.Pedidos.Application.Catalogos;

public sealed record ItemCatalogoDto(int Id, string Nombre, string? Abreviatura = null);

public sealed record ClienteDto(int Id, string NumeroDocumento, string Nombre);

public sealed record ProductoDto(int Id, string? Codigo, string Nombre);

public interface ICatalogoService
{
    Task<IReadOnlyList<ClienteDto>> ClientesAsync(CancellationToken ct);
    Task<IReadOnlyList<ProductoDto>> ProductosAsync(CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> MonedasAsync(CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> SucursalesAsync(CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> TiposDocumentoAsync(CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> UnidadesMedidaAsync(CancellationToken ct);
}

/// <summary>Acota cada catálogo a la empresa del usuario autenticado.</summary>
public sealed class CatalogoService(ICatalogoQueries consultas, ICurrentUser usuarioActual) : ICatalogoService
{
    public Task<IReadOnlyList<ClienteDto>> ClientesAsync(CancellationToken ct) =>
        consultas.ClientesAsync(usuarioActual.EmpresaId, ct);

    public Task<IReadOnlyList<ProductoDto>> ProductosAsync(CancellationToken ct) =>
        consultas.ProductosAsync(usuarioActual.EmpresaId, ct);

    public Task<IReadOnlyList<ItemCatalogoDto>> MonedasAsync(CancellationToken ct) =>
        consultas.MonedasAsync(ct);

    public Task<IReadOnlyList<ItemCatalogoDto>> SucursalesAsync(CancellationToken ct) =>
        consultas.SucursalesAsync(usuarioActual.EmpresaId, ct);

    public Task<IReadOnlyList<ItemCatalogoDto>> TiposDocumentoAsync(CancellationToken ct) =>
        consultas.TiposDocumentoAsync(ct);

    public Task<IReadOnlyList<ItemCatalogoDto>> UnidadesMedidaAsync(CancellationToken ct) =>
        consultas.UnidadesMedidaAsync(ct);
}
