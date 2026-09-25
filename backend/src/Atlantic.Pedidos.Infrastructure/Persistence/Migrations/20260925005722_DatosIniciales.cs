using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlantic.Pedidos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Datos mínimos para usar la aplicación: empresa, catálogos, dos usuarios (Admin y User) y pedidos de ejemplo.
    /// Idempotente: cada fila se inserta sólo si su clave no existe.
    /// Las claves de los usuarios son BCrypt (work factor 11) de "123456", como en el ejemplo del enunciado.
    /// </summary>
    public partial class DatosIniciales : Migration
    {
        private const string Ahora = "GETDATE()";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Lote($"""
                INSERT dbo.Empresa (IDEmpresa, Ruc, RazonSocial, NombreComercial, Direccion, Estado, IDUsuarioCreacion, FechaCreacion)
                SELECT 1, '20601234567', 'ATLANTIC DISTRIBUCIONES S.A.C.', 'Atlantic', 'Av. Javier Prado Este 1234, San Isidro, Lima', 1, 1, {Ahora}
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Empresa WHERE IDEmpresa = 1);

                INSERT dbo.Sucursal (IDSucursal, IDEmpresa, Nombre, Direccion, Estado, EsOficina, IDUsuarioCreacion, FechaCreacion)
                SELECT v.IDSucursal, 1, v.Nombre, v.Direccion, 1, 1, 1, {Ahora}
                FROM (VALUES (1, 'Sede Central - Lima', 'Av. Javier Prado Este 1234, San Isidro'),
                             (2, 'Sucursal Arequipa', 'Calle Mercaderes 215, Cercado de Arequipa')) v(IDSucursal, Nombre, Direccion)
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Sucursal s WHERE s.IDSucursal = v.IDSucursal);
                """);

            InsertarConIdentidad(migrationBuilder, "Moneda", "IDMoneda", "IDMoneda, Nombre, Abreviatura, Estado, IDUsuarioCreacion, FechaCreacion", $"""
                (1, 'Soles', 'PEN', 1, 1, {Ahora}),
                (2, 'Dólares Americanos', 'USD', 1, 1, {Ahora})
                """);

            InsertarConIdentidad(migrationBuilder, "TipoDocumento", "IDTipoDocumento", "IDTipoDocumento, Nombre, Abreviatura, Estado, IDUsuarioCreacion, FechaCreacion", $"""
                (1, 'Documento Nacional de Identidad', 'DNI', 1, 1, {Ahora}),
                (2, 'Registro Único de Contribuyentes', 'RUC', 1, 1, {Ahora})
                """);

            InsertarConIdentidad(migrationBuilder, "UnidadMedida", "IDUnidadMedida", "IDUnidadMedida, Codigo, Nombre, Abreviatura, CodigoSunat, Estado, IDUsuarioCreacion, FechaCreacion", $"""
                (1, 'NIU', 'Unidad', 'UND', 'NIU', 1, 1, {Ahora}),
                (2, 'BX', 'Caja', 'CJA', 'BX', 1, 1, {Ahora})
                """);

            InsertarConIdentidad(migrationBuilder, "Producto", "IDProducto", "IDProducto, IDEmpresa, IDUnidadMedida, CodigoProducto, Nombre, Estado, IDUsuarioCreacion, FechaCreacion", $"""
                (1, 1, 1, 'P001', 'Laptop Lenovo ThinkPad E14 Gen 5', 1, 1, {Ahora}),
                (2, 1, 1, 'P002', 'Monitor LG 27" UltraFine 4K', 1, 1, {Ahora}),
                (3, 1, 1, 'P003', 'Teclado mecánico Logitech MX Mechanical', 1, 1, {Ahora}),
                (4, 1, 1, 'P004', 'Mouse inalámbrico Logitech MX Master 3S', 1, 1, {Ahora}),
                (5, 1, 1, 'P005', 'SSD Kingston NV2 1TB NVMe', 1, 1, {Ahora}),
                (6, 1, 1, 'P006', 'Memoria RAM Kingston Fury 16GB DDR5', 1, 1, {Ahora}),
                (7, 1, 1, 'P007', 'Impresora multifuncional Epson EcoTank L3250', 1, 1, {Ahora}),
                (8, 1, 2, 'P008', 'Papel bond A4 75g (caja x 5 millares)', 1, 1, {Ahora}),
                (9, 1, 1, 'P009', 'Router TP-Link Archer AX55 WiFi 6', 1, 1, {Ahora}),
                (10, 1, 1, 'P010', 'Silla ergonómica Ergo Pro', 1, 1, {Ahora})
                """);

            InsertarConIdentidad(migrationBuilder, "Cliente", "IDCliente", "IDCliente, IDEmpresa, IDTipoDocumento, NumeroDocumento, PrimerNombre, ApellidoPaterno, ApellidoMaterno, RazonSocial, Estado, Eliminado, IDUsuarioCreacion, FechaCreacion", $"""
                (1, 1, 1, '45781236', 'Juan', 'Perez', NULL, NULL, 1, 0, 1, {Ahora}),
                (2, 1, 1, '70214589', 'María', 'Quispe', 'Huamán', NULL, 1, 0, 1, {Ahora}),
                (3, 1, 1, '41236587', 'Carlos', 'Rodríguez', 'Salas', NULL, 1, 0, 1, {Ahora}),
                (4, 1, 1, '46598712', 'Lucía', 'Torres', 'Mendoza', NULL, 1, 0, 1, {Ahora}),
                (5, 1, 2, '20512345678', NULL, NULL, NULL, 'INVERSIONES ANDINAS S.A.C.', 1, 0, 1, {Ahora}),
                (6, 1, 2, '20456789123', NULL, NULL, NULL, 'CORPORACIÓN PACÍFICO DEL SUR S.A.', 1, 0, 1, {Ahora}),
                (7, 1, 2, '20601122334', NULL, NULL, NULL, 'TECNOLOGÍA Y SERVICIOS LIMA E.I.R.L.', 1, 0, 1, {Ahora}),
                (8, 1, 2, '20100234567', NULL, NULL, NULL, 'COMERCIAL SAN MARTÍN S.R.L.', 1, 0, 1, {Ahora})
                """);

            migrationBuilder.Lote($"""
                INSERT dbo.Usuario (IDUsuario, IDEmpresa, Usuario, Clave, NumeroDocumento, NombreCompleto, Email, Rol,
                                    Bloqueado, NumIntentoFallidoLogin, CambiarClave, Eliminado, IDUsuarioCreacion, FechaCreacion)
                SELECT v.IDUsuario, 1, v.Usuario, v.Clave, v.Doc, v.Nombre, v.Email, v.Rol, 0, 0, 0, 0, 1, {Ahora}
                FROM (VALUES
                    (1, 'admin', '$2a$11$MayRe1GDc7Y5HhvaLq/AA.VL4z7k1gh447GME6yspCj.K2N7EwqM.', '10000001', 'Administrador del Sistema', 'admin@email.com', 'Admin'),
                    (2, 'user',  '$2a$11$RomfsP9FFM7PRRHf2GQkiOICSOv.p0MErQy9SLmeSsHsyQdm/iiLG', '10000002', 'Usuario Operador', 'user@email.com', 'User')
                ) v(IDUsuario, Usuario, Clave, Doc, Nombre, Email, Rol)
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Usuario u WHERE u.IDUsuario = v.IDUsuario OR u.Email = v.Email);
                """);

            // (producto, cantidad, precio, descuento)
            InsertarPedido(migrationBuilder, "PED-001", cliente: 1, sucursal: 1, moneda: 1, "2025-01-10", "Registrado",
                (4, 1m, 250.75m, 0m));
            InsertarPedido(migrationBuilder, "PED-002", cliente: 5, sucursal: 1, moneda: 1, "2026-09-01", "Confirmado",
                (1, 2m, 3899.00m, 100m), (4, 2m, 250.75m, 0m));
            InsertarPedido(migrationBuilder, "PED-003", cliente: 2, sucursal: 1, moneda: 1, "2026-09-10", "Despachado",
                (2, 1m, 1459.90m, 0m), (3, 1m, 489.00m, 0m));
            InsertarPedido(migrationBuilder, "PED-004", cliente: 6, sucursal: 1, moneda: 2, "2026-09-15", "Entregado",
                (9, 5m, 129.00m, 0m), (5, 5m, 79.50m, 0m));
            InsertarPedido(migrationBuilder, "PED-005", cliente: 3, sucursal: 1, moneda: 1, "2026-09-20", "Registrado",
                (8, 10m, 145.00m, 50m));
            InsertarPedido(migrationBuilder, "PED-006", cliente: 7, sucursal: 2, moneda: 1, "2026-09-22", "Registrado",
                (10, 4m, 699.00m, 0m), (6, 8m, 219.90m, 0m));
            InsertarPedido(migrationBuilder, "PED-007", cliente: 4, sucursal: 2, moneda: 1, "2026-09-23", "Registrado",
                (7, 1m, 849.00m, 0m));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Lote("""
                DELETE d FROM dbo.PedidoDetalle d JOIN dbo.Pedido p ON p.IDPedido = d.IDPedido
                WHERE p.NumeroPedido IN ('PED-001','PED-002','PED-003','PED-004','PED-005','PED-006','PED-007')
                  AND p.IDUsuarioModificacion IS NULL;
                DELETE FROM dbo.Pedido
                WHERE NumeroPedido IN ('PED-001','PED-002','PED-003','PED-004','PED-005','PED-006','PED-007')
                  AND IDUsuarioModificacion IS NULL;
                DELETE FROM dbo.Usuario WHERE IDUsuario IN (1, 2) AND Email IN ('admin@email.com', 'user@email.com');
                """);
        }

        private static void InsertarConIdentidad(MigrationBuilder mb, string tabla, string clave, string columnas, string valores)
        {
            var aliasColumnas = columnas.Replace(" ", "");
            mb.Lote($"""
                SET IDENTITY_INSERT dbo.{tabla} ON;
                INSERT dbo.{tabla} ({columnas})
                SELECT * FROM (VALUES {valores}) v({aliasColumnas})
                WHERE NOT EXISTS (SELECT 1 FROM dbo.{tabla} t WHERE t.{clave} = v.{clave});
                SET IDENTITY_INSERT dbo.{tabla} OFF;
                """);
        }

        private static void InsertarPedido(MigrationBuilder mb, string numero, int cliente, int sucursal, int moneda,
            string fecha, string estado, params (int Producto, decimal Cantidad, decimal Precio, decimal Descuento)[] lineas)
        {
            static string D(decimal v) => v.ToString(CultureInfo.InvariantCulture);

            var calculadas = lineas.Select((l, i) =>
            {
                var subTotal = Math.Round(l.Cantidad * l.Precio, 2, MidpointRounding.AwayFromZero);
                return (Item: i + 1, l.Producto, l.Cantidad, l.Precio, l.Descuento, SubTotal: subTotal, Importe: subTotal - l.Descuento);
            }).ToList();

            var detalle = string.Join(",\n", calculadas.Select(l =>
                $"(@id, {l.Producto}, {l.Item}, {D(l.Cantidad)}, {D(l.Precio)}, {D(l.Descuento)}, {D(l.SubTotal)}, {D(l.Importe)}, 1, {Ahora}, 0)"));

            mb.Lote($"""
                IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = '{numero}')
                BEGIN
                    DECLARE @id int;
                    INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                                       Estado, IDUsuarioCreacion, FechaCreacion)
                    VALUES ({sucursal}, {cliente}, {moneda}, '{numero}', '{fecha.Replace("-", "")}',
                            {D(calculadas.Sum(l => l.Descuento))}, {D(calculadas.Sum(l => l.Importe))}, '{estado}', 1, {Ahora});
                    SET @id = SCOPE_IDENTITY();

                    INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                              IDUsuarioCreacion, FechaCreacion, Eliminado)
                    VALUES {detalle};
                END
                """);
        }
    }
}
