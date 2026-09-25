using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Usuarios;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Pedidos;
using Atlantic.Pedidos.Domain.Usuarios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Atlantic.Pedidos.Infrastructure.Persistence;

internal sealed class UsuarioRepository(AppDbContext db) : IUsuarioRepository
{
    // La collation de la BD es CI: la comparación ya ignora mayúsculas.
    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObtenerAsync(int id, int empresaId, CancellationToken ct) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.EmpresaId == empresaId, ct);

    // El email es único en toda la tabla (UX_Usuario_Email), no por empresa: es la llave del login.
    public Task<bool> ExisteEmailAsync(string email, int? excluirId, CancellationToken ct) =>
        db.Usuarios.AnyAsync(u => u.Email == email && (excluirId == null || u.Id != excluirId), ct);

    public Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? excluirId, CancellationToken ct) =>
        db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario && (excluirId == null || u.Id != excluirId), ct);

    public Task<int> ContarAdminsActivosAsync(int empresaId, int? excluirId, CancellationToken ct) =>
        db.Usuarios.CountAsync(u => u.EmpresaId == empresaId && u.Rol == Roles.Admin && u.Eliminado != true
                                    && (excluirId == null || u.Id != excluirId), ct);

    public async Task<int> SiguienteIdAsync(CancellationToken ct) =>
        (await db.Usuarios.MaxAsync(u => (int?)u.Id, ct) ?? 0) + 1;

    public void Agregar(Usuario usuario) => db.Usuarios.Add(usuario);
}

internal sealed class UsuarioQueries(AppDbContext db) : IUsuarioQueries
{
    public async Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(int empresaId, string? buscar, CancellationToken ct)
    {
        var query = db.Usuarios.AsNoTracking().Where(u => u.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            query = query.Where(u => u.NombreCompleto.Contains(texto) || u.Email.Contains(texto) || u.NombreUsuario.Contains(texto));
        }

        return await query
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new UsuarioAdminDto(u.Id, u.NombreUsuario, u.NombreCompleto, u.NumeroDocumento, u.Email, u.Rol,
                u.Eliminado != true, u.Bloqueado, u.NumIntentoFallidoLogin ?? 0, u.FechaUltimoLogin, u.FechaCreacion))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Estado de sesión con caché corta: el control se ejecuta en cada petición autenticada y no conviene
/// una consulta por request. Los cambios del administrador invalidan la entrada y se aplican al instante;
/// cualquier otro cambio (p. ej. un bloqueo por intentos) se refleja como máximo en <see cref="Duracion"/>.
/// </summary>
internal sealed class EstadoSesionProvider(AppDbContext db, IMemoryCache cache) : IEstadoSesionProvider
{
    private static readonly TimeSpan Duracion = TimeSpan.FromSeconds(30);

    private static string Clave(int usuarioId) => $"sesion:{usuarioId}";

    public async Task<EstadoSesion?> ObtenerAsync(int usuarioId, CancellationToken ct)
    {
        if (cache.TryGetValue(Clave(usuarioId), out EstadoSesion? enCache))
            return enCache;

        var estado = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new EstadoSesion(u.Eliminado != true, u.Bloqueado, u.FechaBloqueo, u.Rol, u.EmpresaId, u.FechaUltimoCambiarClave))
            .FirstOrDefaultAsync(ct);

        cache.Set(Clave(usuarioId), estado, Duracion);
        return estado;
    }

    public void Invalidar(int usuarioId) => cache.Remove(Clave(usuarioId));
}

internal sealed class PedidoRepository(AppDbContext db) : IPedidoRepository
{
    public Task<Pedido?> ObtenerAsync(int id, int empresaId, CancellationToken ct) =>
        db.Pedidos
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == id && p.Sucursal!.EmpresaId == empresaId, ct);

    public Task<bool> ExisteNumeroAsync(string numeroPedido, int? excluirId, CancellationToken ct) =>
        db.Pedidos
            .IgnoreQueryFilters() // los anulados también reservan su número
            .AnyAsync(p => p.NumeroPedido == numeroPedido && (excluirId == null || p.Id != excluirId), ct);

    public void Agregar(Pedido pedido) => db.Pedidos.Add(pedido);
}

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("PEDIDO_MODIFICADO",
                "Otro usuario modificó o eliminó el registro mientras lo editaba. Recargue los datos e intente nuevamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException
                                           {
                                               Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation
                                           } sql)
        {
            // Carrera entre dos altas con el mismo número: la validación previa pasó en ambas,
            // el índice único detiene a la segunda.
            if (sql.Message.Contains("UX_Pedido_NumeroPedido", StringComparison.OrdinalIgnoreCase))
                throw new ConflictException("PEDIDO_NUMERO_DUPLICADO", "Ya existe un pedido con ese número.");

            throw new ConflictException("REGISTRO_DUPLICADO", "Ya existe un registro con ese valor único.");
        }
    }
}
