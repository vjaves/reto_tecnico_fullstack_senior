using Microsoft.EntityFrameworkCore.Migrations;

namespace Atlantic.Pedidos.Infrastructure.Persistence.Migrations;

internal static class MigrationSql
{
    /// <summary>
    /// Ejecuta el bloque dentro de <c>EXEC(N'...')</c>.
    /// </summary>
    /// <remarks>
    /// Al migrar en vivo, EF manda cada <c>Sql()</c> como un comando aparte; pero el script
    /// <c>--idempotent</c> los junta en un solo batch. Ahí fallan dos cosas: un <c>ALTER TABLE ADD</c>
    /// seguido de un CHECK sobre esa columna no compila (la columna aún no existe), y los
    /// <c>DECLARE</c> de distintos bloques chocan entre sí. Con EXEC cada bloque se compila al
    /// ejecutarse y tiene su propio ámbito, así que el script y la migración en vivo se comportan igual.
    /// </remarks>
    public static void Lote(this MigrationBuilder migrationBuilder, string sql) =>
        migrationBuilder.Sql($"EXEC(N'{sql.Replace("'", "''")}');");
}
