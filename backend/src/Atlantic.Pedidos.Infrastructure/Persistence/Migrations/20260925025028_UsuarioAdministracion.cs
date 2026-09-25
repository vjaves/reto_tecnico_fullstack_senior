using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlantic.Pedidos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Sin cambios de esquema: el panel de administración empieza a usar columnas de dbo.Usuario que el modelo
    /// referencial ya tenía (NumeroDocumento, FechaUltimoCambiarClave y auditoría). La migración sólo
    /// sincroniza el modelo de EF; sin ella, EF 9 rechaza MigrateAsync por "cambios de modelo pendientes".
    /// </summary>
    public partial class UsuarioAdministracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
