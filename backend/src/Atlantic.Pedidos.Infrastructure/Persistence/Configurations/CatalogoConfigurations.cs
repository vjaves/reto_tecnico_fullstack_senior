using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlantic.Pedidos.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuario", t => t.HasCheckConstraint("CK_Usuario_Rol", "[Rol] IN ('Admin','User')"));

        b.HasKey(u => u.Id);
        // IDUsuario no es identity en el modelo referencial.
        b.Property(u => u.Id).HasColumnName("IDUsuario").ValueGeneratedNever();
        b.Property(u => u.EmpresaId).HasColumnName("IDEmpresa");
        b.Property(u => u.NombreUsuario).HasColumnName("Usuario").HasMaxLength(50).IsUnicode(false);
        b.Property(u => u.ClaveHash).HasColumnName("Clave").HasMaxLength(100).IsUnicode(false);
        b.Property(u => u.NombreCompleto).HasMaxLength(300).IsUnicode(false);
        b.Property(u => u.Email).HasMaxLength(300).IsUnicode(false);
        b.Property(u => u.Rol).HasMaxLength(20).IsUnicode(false);
        b.Property(u => u.FechaBloqueo).HasColumnType("datetime");
        b.Property(u => u.FechaUltimoIntentoLogin).HasColumnType("datetime");
        b.Property(u => u.FechaUltimoLogin).HasColumnType("datetime");
        b.Property(u => u.FechaUltimoCambiarClave).HasColumnType("datetime");
        b.Property(u => u.NumeroDocumento).HasMaxLength(20).IsUnicode(false);
        b.Property(u => u.UsuarioCreacionId).HasColumnName("IDUsuarioCreacion");
        b.Property(u => u.FechaCreacion).HasColumnType("datetime");
        b.Property(u => u.UsuarioModificacionId).HasColumnName("IDUsuarioModificacion");
        b.Property(u => u.FechaModificacion).HasColumnType("datetime");

        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UX_Usuario_Email");

        b.Ignore(u => u.EstaActivo);
    }
}

// Cliente y Producto NO llevan filtro global de eliminados: Pedido los referencia como navegación
// requerida, y EF convertiría el filtro en un INNER JOIN que haría desaparecer los pedidos
// históricos de un cliente o producto dado de baja. Cada consulta filtra explícitamente.

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Cliente");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("IDCliente").UseIdentityColumn();
        b.Property(c => c.EmpresaId).HasColumnName("IDEmpresa");
        b.Property(c => c.TipoDocumentoId).HasColumnName("IDTipoDocumento");
        b.Property(c => c.NumeroDocumento).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.ApellidoPaterno).HasMaxLength(100).IsUnicode(false);
        b.Property(c => c.ApellidoMaterno).HasMaxLength(100).IsUnicode(false);
        b.Property(c => c.PrimerNombre).HasMaxLength(100).IsUnicode(false);
        b.Property(c => c.SegundoNombre).HasMaxLength(100).IsUnicode(false);
        b.Property(c => c.RazonSocial).HasMaxLength(200).IsUnicode(false);
        b.Property(c => c.NombreComercial).HasMaxLength(100).IsUnicode(false);
        b.Property(c => c.Direccion).HasMaxLength(200).IsUnicode(false);
        b.Property(c => c.Celular).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.UsuarioCreacionId).HasColumnName("IDUsuarioCreacion");
        b.Property(c => c.FechaCreacion).HasColumnType("datetime");
        b.Property(c => c.UsuarioModificacionId).HasColumnName("IDUsuarioModificacion");
        b.Property(c => c.FechaModificacion).HasColumnType("datetime");
        b.Property(c => c.UsuarioAnulacionId).HasColumnName("IDUsuarioAnulacion");
        b.Property(c => c.FechaAnulacion).HasColumnType("datetime");

        b.HasIndex(c => new { c.EmpresaId, c.NumeroDocumento }).HasDatabaseName("IX_Cliente_Empresa_Documento");
        b.HasOne(c => c.TipoDocumento).WithMany().HasForeignKey(c => c.TipoDocumentoId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(c => c.EstaEliminado);
    }
}

internal sealed class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Producto");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("IDProducto").UseIdentityColumn();
        b.Property(p => p.EmpresaId).HasColumnName("IDEmpresa");
        b.Property(p => p.UnidadMedidaId).HasColumnName("IDUnidadMedida");
        b.Property(p => p.CodigoProducto).HasMaxLength(50).IsUnicode(false);
        b.Property(p => p.Nombre).HasMaxLength(1000).IsUnicode(false);
        b.Property(p => p.Descripcion).HasMaxLength(8000).IsUnicode(false);
        b.Property(p => p.UsuarioCreacionId).HasColumnName("IDUsuarioCreacion");
        b.Property(p => p.FechaCreacion).HasColumnType("datetime");
        b.Property(p => p.UsuarioModificacionId).HasColumnName("IDUsuarioModificacion");
        b.Property(p => p.FechaModificacion).HasColumnType("datetime");

        b.HasIndex(p => new { p.EmpresaId, p.CodigoProducto }).HasDatabaseName("IX_Producto_Empresa_Codigo");
        b.HasOne(p => p.UnidadMedida).WithMany().HasForeignKey(p => p.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TipoDocumentoConfiguration : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> b)
    {
        b.ToTable("TipoDocumento");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("IDTipoDocumento").UseIdentityColumn();
        b.Property(t => t.Nombre).HasMaxLength(100).IsUnicode(false);
        b.Property(t => t.Abreviatura).HasMaxLength(5).IsUnicode(false);
    }
}

internal sealed class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> b)
    {
        b.ToTable("UnidadMedida");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("IDUnidadMedida").UseIdentityColumn();
        b.Property(u => u.Codigo).HasMaxLength(3).IsUnicode(false);
        b.Property(u => u.Nombre).HasMaxLength(100).IsUnicode(false);
        b.Property(u => u.Abreviatura).HasMaxLength(5).IsUnicode(false);
    }
}

internal sealed class MonedaConfiguration : IEntityTypeConfiguration<Moneda>
{
    public void Configure(EntityTypeBuilder<Moneda> b)
    {
        b.ToTable("Moneda");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("IDMoneda").UseIdentityColumn();
        b.Property(m => m.Nombre).HasMaxLength(100).IsUnicode(false);
        b.Property(m => m.Abreviatura).HasMaxLength(5).IsUnicode(false);
    }
}

internal sealed class SucursalConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> b)
    {
        b.ToTable("Sucursal");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("IDSucursal").ValueGeneratedNever();
        b.Property(s => s.EmpresaId).HasColumnName("IDEmpresa");
        b.Property(s => s.Nombre).HasMaxLength(200).IsUnicode(false);
    }
}
