using Atlantic.Pedidos.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlantic.Pedidos.Infrastructure.Persistence.Configurations;

// Las tablas vienen del modelo referencial (columnas IDxxx, datetime, varchar). El mapeo
// respeta esos nombres y tipos; el dominio usa nombres .NET idiomáticos.

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.ToTable("Pedido", t =>
        {
            t.HasCheckConstraint("CK_Pedido_TotalPedido", "[TotalPedido] > 0");
            t.HasCheckConstraint("CK_Pedido_Estado",
                "[Estado] IN ('Registrado','Confirmado','Despachado','Entregado','Anulado')");
            t.HasCheckConstraint("CK_Pedido_Anulacion",
                "([Estado] = 'Anulado' AND [FechaAnulado] IS NOT NULL) OR ([Estado] <> 'Anulado' AND [FechaAnulado] IS NULL)");
        });

        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("IDPedido").UseIdentityColumn();
        b.Property(p => p.SucursalId).HasColumnName("IDSucursal");
        b.Property(p => p.ClienteId).HasColumnName("IDCliente");
        b.Property(p => p.MonedaId).HasColumnName("IDMoneda");
        b.Property(p => p.NumeroPedido).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(p => p.FechaPedido).HasColumnType("datetime");
        b.Property(p => p.TotalDescuentos).HasPrecision(12, 2);
        b.Property(p => p.TotalPedido).HasPrecision(12, 2);
        b.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(p => p.UsuarioCreacionId).HasColumnName("IDUsuarioCreacion");
        b.Property(p => p.FechaCreacion).HasColumnType("datetime");
        b.Property(p => p.UsuarioModificacionId).HasColumnName("IDUsuarioModificacion");
        b.Property(p => p.FechaModificacion).HasColumnType("datetime");
        b.Property(p => p.UsuarioAnuladoId).HasColumnName("IDUsuarioAnulado");
        b.Property(p => p.FechaAnulado).HasColumnType("datetime");
        b.Property(p => p.RowVersion).IsRowVersion();

        b.HasIndex(p => p.NumeroPedido).IsUnique().HasDatabaseName("UX_Pedido_NumeroPedido");
        b.HasIndex(p => p.FechaPedido).HasDatabaseName("IX_Pedido_FechaPedido");
        b.HasIndex(p => p.ClienteId).HasDatabaseName("IX_Pedido_IDCliente");

        b.HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Moneda).WithMany().HasForeignKey(p => p.MonedaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Sucursal).WithMany().HasForeignKey(p => p.SucursalId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(p => p.Detalles).WithOne().HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(p => p.Detalles).HasField("_detalles").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(p => p.EstaAnulado);

        // Eliminación lógica: un pedido anulado no existe para el CRUD.
        b.HasQueryFilter(p => p.FechaAnulado == null);
    }
}

internal sealed class PedidoDetalleConfiguration : IEntityTypeConfiguration<PedidoDetalle>
{
    public void Configure(EntityTypeBuilder<PedidoDetalle> b)
    {
        b.ToTable("PedidoDetalle");

        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("IDPedidoDetalle").UseIdentityColumn();
        b.Property(d => d.PedidoId).HasColumnName("IDPedido");
        b.Property(d => d.ProductoId).HasColumnName("IDProducto");
        b.Property(d => d.Cantidad).HasColumnType("money");
        b.Property(d => d.PrecioVenta).HasPrecision(18, 6);
        b.Property(d => d.Descuento).HasPrecision(12, 2);
        b.Property(d => d.SubTotal).HasPrecision(12, 2);
        b.Property(d => d.ImporteTotal).HasPrecision(18, 2);
        b.Property(d => d.UsuarioCreacionId).HasColumnName("IDUsuarioCreacion");
        b.Property(d => d.FechaCreacion).HasColumnType("datetime");
        b.Property(d => d.UsuarioModificacionId).HasColumnName("IDUsuarioModificacion");
        b.Property(d => d.FechaModificacion).HasColumnType("datetime");

        b.HasIndex(d => d.PedidoId).HasDatabaseName("IX_PedidoDetalle_IDPedido");

        b.HasOne(d => d.Producto).WithMany().HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(d => !d.Eliminado);
    }
}
