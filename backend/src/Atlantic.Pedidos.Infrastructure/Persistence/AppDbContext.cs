using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Pedidos;
using Atlantic.Pedidos.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Atlantic.Pedidos.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoDetalle> PedidoDetalles => Set<PedidoDetalle>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Moneda> Monedas => Set<Moneda>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
