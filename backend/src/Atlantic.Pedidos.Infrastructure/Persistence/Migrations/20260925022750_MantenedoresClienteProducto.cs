using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlantic.Pedidos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Mantenedores de clientes y productos. El modelo EF recién mapea columnas que el modelo referencial
    /// ya tenía (Direccion, Celular, auditoría, IDUnidadMedida…) y las tablas TipoDocumento/UnidadMedida;
    /// el diff de EF las da por nuevas, pero en BD ya existen. Aquí sólo va lo realmente nuevo.
    /// </summary>
    public partial class MantenedoresClienteProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Producto sólo tenía Estado (activo/inactivo); la baja lógica necesita su propia marca
            // para distinguir "desactivado, se puede reactivar" de "eliminado".
            migrationBuilder.Lote("""
                IF COL_LENGTH(N'dbo.Producto', N'Eliminado') IS NULL
                ALTER TABLE dbo.Producto ADD Eliminado bit NOT NULL
                    CONSTRAINT DF_Producto_Eliminado DEFAULT (0);
                """);

            // Búsquedas de duplicados por empresa (documento del cliente, código del producto).
            // No son únicos: los registros eliminados conservan su documento/código.
            migrationBuilder.Lote("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Cliente_Empresa_Documento' AND object_id = OBJECT_ID(N'dbo.Cliente'))
                CREATE INDEX IX_Cliente_Empresa_Documento ON dbo.Cliente (IDEmpresa, NumeroDocumento);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Producto_Empresa_Codigo' AND object_id = OBJECT_ID(N'dbo.Producto'))
                CREATE INDEX IX_Producto_Empresa_Codigo ON dbo.Producto (IDEmpresa, CodigoProducto);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Lote("""
                DROP INDEX IF EXISTS IX_Producto_Empresa_Codigo ON dbo.Producto;
                DROP INDEX IF EXISTS IX_Cliente_Empresa_Documento ON dbo.Cliente;
                ALTER TABLE dbo.Producto DROP CONSTRAINT IF EXISTS DF_Producto_Eliminado;
                ALTER TABLE dbo.Producto DROP COLUMN IF EXISTS Eliminado;
                """);
        }
    }
}
