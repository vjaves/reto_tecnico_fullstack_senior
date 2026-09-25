using Atlantic.Pedidos.Application.Catalogos;
using Atlantic.Pedidos.Application.Clientes;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Pedidos;
using Atlantic.Pedidos.Application.Productos;
using Atlantic.Pedidos.Application.Usuarios;
using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Pedidos;
using Atlantic.Pedidos.Domain.Usuarios;

namespace Atlantic.Pedidos.Application.Abstractions;

// Puertos que la capa de aplicación necesita del exterior. Infrastructure los implementa;
// Application nunca referencia EF Core, JWT ni BCrypt directamente.

/// <summary>Usuario autenticado de la petición en curso.</summary>
public interface ICurrentUser
{
    int UsuarioId { get; }
    int EmpresaId { get; }
    string Rol { get; }
}

public interface IUnitOfWork
{
    /// <summary>
    /// Persiste los cambios. Traduce la violación de índice único y el choque de concurrencia
    /// a <see cref="ConflictException"/>.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed record TokenGenerado(string Token, int ExpiresInSeconds, DateTime ExpiraEnUtc);

public interface IJwtTokenGenerator
{
    TokenGenerado Generar(Usuario usuario);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct);
    Task<Usuario?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<bool> ExisteEmailAsync(string email, int? excluirId, CancellationToken ct);
    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? excluirId, CancellationToken ct);
    Task<int> ContarAdminsActivosAsync(int empresaId, int? excluirId, CancellationToken ct);
    Task<int> SiguienteIdAsync(CancellationToken ct);
    void Agregar(Usuario usuario);
}

public interface IUsuarioQueries
{
    Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(int empresaId, string? buscar, CancellationToken ct);
}

/// <summary>
/// Estado de la cuenta para el control de sesión. La implementación lo cachea unos segundos para no
/// consultar la BD en cada petición; <see cref="Invalidar"/> aplica un cambio del administrador al instante.
/// </summary>
public interface IEstadoSesionProvider
{
    Task<EstadoSesion?> ObtenerAsync(int usuarioId, CancellationToken ct);
    void Invalidar(int usuarioId);
}

/// <summary>Lado de escritura del agregado Pedido (entidades con tracking).</summary>
public interface IPedidoRepository
{
    Task<Pedido?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<bool> ExisteNumeroAsync(string numeroPedido, int? excluirId, CancellationToken ct);
    void Agregar(Pedido pedido);
}

/// <summary>Lado de lectura: proyecciones directas a DTO, sin tracking.</summary>
public interface IPedidoQueries
{
    Task<PagedResult<PedidoResumenDto>> ListarAsync(int empresaId, PedidoFiltro filtro, CancellationToken ct);
    Task<PedidoDto?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<string> SiguienteNumeroAsync(CancellationToken ct);
    Task<PedidoResumenEstadosDto> ResumenAsync(int empresaId, CancellationToken ct);
}

public interface IClienteRepository
{
    /// <summary>Cliente no eliminado de la empresa, con tracking.</summary>
    Task<Cliente?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(int empresaId, int tipoDocumentoId, string numero, int? excluirId, CancellationToken ct);
    void Agregar(Cliente cliente);
}

public interface IClienteQueries
{
    Task<PagedResult<ClienteResumenDto>> ListarAsync(int empresaId, MaestroFiltro filtro, CancellationToken ct);
    Task<ClienteDetalleDto?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<int> PedidosEnCursoAsync(int clienteId, CancellationToken ct);
}

public interface IProductoRepository
{
    /// <summary>Producto no eliminado de la empresa, con tracking.</summary>
    Task<Producto?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<bool> ExisteCodigoAsync(int empresaId, string codigo, int? excluirId, CancellationToken ct);
    void Agregar(Producto producto);
}

public interface IProductoQueries
{
    Task<PagedResult<ProductoResumenDto>> ListarAsync(int empresaId, MaestroFiltro filtro, CancellationToken ct);
    Task<ProductoDetalleDto?> ObtenerAsync(int id, int empresaId, CancellationToken ct);
    Task<int> PedidosEnCursoAsync(int productoId, CancellationToken ct);
}

public interface ICatalogoQueries
{
    Task<IReadOnlyList<ClienteDto>> ClientesAsync(int empresaId, CancellationToken ct);
    Task<IReadOnlyList<ProductoDto>> ProductosAsync(int empresaId, CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> MonedasAsync(CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> SucursalesAsync(int empresaId, CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> TiposDocumentoAsync(CancellationToken ct);
    Task<IReadOnlyList<ItemCatalogoDto>> UnidadesMedidaAsync(CancellationToken ct);

    Task<TipoDocumento?> TipoDocumentoAsync(int id, CancellationToken ct);
    Task<bool> UnidadMedidaValidaAsync(int id, CancellationToken ct);

    Task<bool> ClienteValidoAsync(int empresaId, int clienteId, CancellationToken ct);
    Task<bool> SucursalValidaAsync(int empresaId, int sucursalId, CancellationToken ct);
    Task<bool> MonedaValidaAsync(int monedaId, CancellationToken ct);

    /// <summary>De los ids recibidos, devuelve los que existen, están activos y son de la empresa.</summary>
    Task<IReadOnlySet<int>> ProductosValidosAsync(int empresaId, IReadOnlyCollection<int> productoIds, CancellationToken ct);
}
