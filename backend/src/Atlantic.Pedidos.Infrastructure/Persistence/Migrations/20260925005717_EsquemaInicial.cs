using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlantic.Pedidos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Las tablas del modelo referencial pueden existir antes que la aplicación (BDExamenAtlantic ya las trae).
    /// Por eso esta migración no usa CreateTable: la línea base se crea sólo si falta, y luego se agregan
    /// las piezas que el reto exige y el modelo no tenía (estado, concurrencia, rol, unicidad, CHECKs).
    /// Cada bloque es idempotente para que el script generado se pueda correr más de una vez.
    /// </summary>
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1. Línea base: modelo referencial ----------
            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Empresa', N'U') IS NULL
                CREATE TABLE dbo.Empresa (
                    IDEmpresa int NOT NULL CONSTRAINT PK_Empresa PRIMARY KEY,
                    Ruc varchar(20) NOT NULL,
                    RazonSocial varchar(200) NOT NULL,
                    NombreComercial varchar(100) NULL,
                    Direccion varchar(150) NULL,
                    Estado bit NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Sucursal', N'U') IS NULL
                CREATE TABLE dbo.Sucursal (
                    IDSucursal int NOT NULL CONSTRAINT PK_Sucursal PRIMARY KEY,
                    IDEmpresa int NOT NULL CONSTRAINT FK_Sucursal_Empresa REFERENCES dbo.Empresa (IDEmpresa),
                    Nombre varchar(200) NOT NULL,
                    Telefono varchar(50) NULL, Celular varchar(50) NULL, Email varchar(100) NULL,
                    IDUbigeo varchar(10) NULL, Direccion varchar(500) NULL,
                    Estado bit NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL,
                    EsOficina bit NULL, EsAutomatico bit NULL, PuertaEmbarque varchar(200) NULL,
                    Color varchar(50) NULL, TokenImpresionDirecta varchar(100) NULL,
                    CodigoEstablecimientoSunat varchar(4) NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Moneda', N'U') IS NULL
                CREATE TABLE dbo.Moneda (
                    IDMoneda int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Moneda PRIMARY KEY,
                    Nombre varchar(100) NOT NULL,
                    Abreviatura varchar(5) NULL,
                    Estado bit NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.UnidadMedida', N'U') IS NULL
                CREATE TABLE dbo.UnidadMedida (
                    IDUnidadMedida int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UnidadMedida PRIMARY KEY,
                    Codigo varchar(3) NOT NULL,
                    Nombre varchar(100) NOT NULL,
                    Abreviatura varchar(5) NULL,
                    CodigoSunat varchar(10) NULL,
                    Estado bit NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Producto', N'U') IS NULL
                CREATE TABLE dbo.Producto (
                    IDProducto int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Producto PRIMARY KEY,
                    IDEmpresa int NULL,
                    IDUnidadMedida int NULL CONSTRAINT FK_Producto_UnidadMedida REFERENCES dbo.UnidadMedida (IDUnidadMedida),
                    CodigoProducto varchar(50) NULL,
                    Nombre varchar(1000) NOT NULL,
                    Descripcion varchar(8000) NULL,
                    Estado bit NOT NULL,
                    IDUsuarioCreacion int NOT NULL, FechaCreacion datetime NOT NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.TipoDocumento', N'U') IS NULL
                CREATE TABLE dbo.TipoDocumento (
                    IDTipoDocumento int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TipoDocumento PRIMARY KEY,
                    Nombre varchar(100) NOT NULL,
                    Abreviatura varchar(5) NULL,
                    Estado bit NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Cliente', N'U') IS NULL
                CREATE TABLE dbo.Cliente (
                    IDCliente int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cliente PRIMARY KEY,
                    IDEmpresa int NOT NULL CONSTRAINT FK_Cliente_Empresa REFERENCES dbo.Empresa (IDEmpresa),
                    IDTipoDocumento int NOT NULL CONSTRAINT FK_Cliente_TipoDocumento REFERENCES dbo.TipoDocumento (IDTipoDocumento),
                    NumeroDocumento varchar(20) NOT NULL,
                    ApellidoPaterno varchar(100) NULL, ApellidoMaterno varchar(100) NULL,
                    PrimerNombre varchar(100) NULL, SegundoNombre varchar(100) NULL,
                    RazonSocial varchar(200) NULL, NombreComercial varchar(100) NULL,
                    Direccion varchar(200) NULL, Celular varchar(20) NULL,
                    Estado bit NULL, Eliminado bit NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL,
                    IDUsuarioAnulacion int NULL, FechaAnulacion datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Pedido', N'U') IS NULL
                CREATE TABLE dbo.Pedido (
                    IDPedido int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Pedido PRIMARY KEY,
                    IDSucursal int NOT NULL CONSTRAINT FK_Pedido_Sucursal REFERENCES dbo.Sucursal (IDSucursal),
                    IDCliente int NOT NULL CONSTRAINT FK_Pedido_Cliente REFERENCES dbo.Cliente (IDCliente),
                    IDMoneda int NOT NULL CONSTRAINT FK_Pedido_Moneda REFERENCES dbo.Moneda (IDMoneda),
                    NumeroPedido varchar(20) NOT NULL,
                    FechaPedido datetime NOT NULL,
                    TotalDescuentos decimal(12,2) NULL,
                    TotalPedido decimal(12,2) NOT NULL,
                    IDUsuarioCreacion int NOT NULL, FechaCreacion datetime NOT NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL,
                    IDUsuarioAnulado int NULL, FechaAnulado datetime NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.PedidoDetalle', N'U') IS NULL
                CREATE TABLE dbo.PedidoDetalle (
                    IDPedidoDetalle int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PedidoDetalle PRIMARY KEY,
                    IDPedido int NOT NULL CONSTRAINT FK_PedidoDetalle_Pedido REFERENCES dbo.Pedido (IDPedido),
                    IDProducto int NOT NULL CONSTRAINT FK_PedidoDetalle_Producto REFERENCES dbo.Producto (IDProducto),
                    Item int NOT NULL,
                    Cantidad money NULL,
                    PrecioVenta decimal(18,6) NULL,
                    Descuento decimal(12,2) NULL,
                    SubTotal decimal(12,2) NULL,
                    ImporteTotal decimal(18,2) NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL,
                    Eliminado bit NULL);
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
                CREATE TABLE dbo.Usuario (
                    IDUsuario int NOT NULL CONSTRAINT PK_Usuario PRIMARY KEY,
                    IDEmpresa int NULL CONSTRAINT FK_Usuario_Empresa REFERENCES dbo.Empresa (IDEmpresa),
                    Usuario varchar(50) NOT NULL,
                    Clave varchar(100) NOT NULL,
                    NumeroDocumento varchar(20) NOT NULL,
                    NombreCompleto varchar(300) NOT NULL,
                    Email varchar(300) NOT NULL,
                    Bloqueado bit NOT NULL,
                    FechaBloqueo datetime NULL,
                    NumIntentoFallidoLogin int NULL,
                    FechaUltimoIntentoLogin datetime NULL,
                    FechaBajaInactividad datetime NULL,
                    CambiarClave bit NULL,
                    NumIntentoRecuperarClave int NULL,
                    FechaUltimoIntentoRecuperarClave datetime NULL,
                    FechaUltimoLogin datetime NULL,
                    FechaUltimoCambiarClave datetime NULL,
                    ActualizarDatos bit NULL,
                    IDEstado int NULL,
                    IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
                    IDUsuarioModificacion int NULL, FechaModificacion datetime NULL,
                    Eliminado bit NULL);
                """);

            // ---------- 2. Pedido: estado, concurrencia y reglas de negocio en BD ----------
            migrationBuilder.Lote("""
                IF COL_LENGTH(N'dbo.Pedido', N'Estado') IS NULL
                ALTER TABLE dbo.Pedido ADD Estado varchar(20) NOT NULL
                    CONSTRAINT DF_Pedido_Estado DEFAULT ('Registrado');
                """);

            migrationBuilder.Lote("""
                IF COL_LENGTH(N'dbo.Pedido', N'RowVersion') IS NULL
                ALTER TABLE dbo.Pedido ADD RowVersion rowversion NOT NULL;
                """);

            // Si hubiera pedidos anulados de antes, se alinea el estado con la fecha de anulación.
            migrationBuilder.Lote("""
                UPDATE dbo.Pedido SET Estado = 'Anulado' WHERE FechaAnulado IS NOT NULL AND Estado <> 'Anulado';
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.CK_Pedido_Estado', N'C') IS NULL
                ALTER TABLE dbo.Pedido ADD CONSTRAINT CK_Pedido_Estado
                    CHECK ([Estado] IN ('Registrado','Confirmado','Despachado','Entregado','Anulado'));

                IF OBJECT_ID(N'dbo.CK_Pedido_TotalPedido', N'C') IS NULL
                ALTER TABLE dbo.Pedido ADD CONSTRAINT CK_Pedido_TotalPedido CHECK ([TotalPedido] > 0);

                IF OBJECT_ID(N'dbo.CK_Pedido_Anulacion', N'C') IS NULL
                ALTER TABLE dbo.Pedido ADD CONSTRAINT CK_Pedido_Anulacion
                    CHECK (([Estado] = 'Anulado' AND [FechaAnulado] IS NOT NULL)
                        OR ([Estado] <> 'Anulado' AND [FechaAnulado] IS NULL));

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Pedido_NumeroPedido' AND object_id = OBJECT_ID(N'dbo.Pedido'))
                CREATE UNIQUE INDEX UX_Pedido_NumeroPedido ON dbo.Pedido (NumeroPedido);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedido_FechaPedido' AND object_id = OBJECT_ID(N'dbo.Pedido'))
                CREATE INDEX IX_Pedido_FechaPedido ON dbo.Pedido (FechaPedido);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedido_IDCliente' AND object_id = OBJECT_ID(N'dbo.Pedido'))
                CREATE INDEX IX_Pedido_IDCliente ON dbo.Pedido (IDCliente);
                """);

            // ---------- 3. PedidoDetalle: la baja lógica de líneas necesita Eliminado no nulo ----------
            migrationBuilder.Lote("""
                UPDATE dbo.PedidoDetalle SET Eliminado = 0 WHERE Eliminado IS NULL;

                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.PedidoDetalle') AND name = N'Eliminado' AND is_nullable = 1)
                ALTER TABLE dbo.PedidoDetalle ALTER COLUMN Eliminado bit NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.PedidoDetalle')
                               AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.PedidoDetalle'), N'Eliminado', 'ColumnId'))
                ALTER TABLE dbo.PedidoDetalle ADD CONSTRAINT DF_PedidoDetalle_Eliminado DEFAULT (0) FOR Eliminado;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PedidoDetalle_IDPedido' AND object_id = OBJECT_ID(N'dbo.PedidoDetalle'))
                CREATE INDEX IX_PedidoDetalle_IDPedido ON dbo.PedidoDetalle (IDPedido);
                """);

            // ---------- 4. Usuario: rol para autorización y email único para el login ----------
            migrationBuilder.Lote("""
                IF COL_LENGTH(N'dbo.Usuario', N'Rol') IS NULL
                ALTER TABLE dbo.Usuario ADD Rol varchar(20) NOT NULL
                    CONSTRAINT DF_Usuario_Rol DEFAULT ('User');
                """);

            migrationBuilder.Lote("""
                IF OBJECT_ID(N'dbo.CK_Usuario_Rol', N'C') IS NULL
                ALTER TABLE dbo.Usuario ADD CONSTRAINT CK_Usuario_Rol CHECK ([Rol] IN ('Admin','User'));

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Usuario_Email' AND object_id = OBJECT_ID(N'dbo.Usuario'))
                CREATE UNIQUE INDEX UX_Usuario_Email ON dbo.Usuario (Email);
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Revierte sólo lo que esta migración agregó. Las tablas del modelo referencial no se eliminan:
        /// pueden haber existido antes de la aplicación.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Lote("""
                DROP INDEX IF EXISTS UX_Usuario_Email ON dbo.Usuario;
                ALTER TABLE dbo.Usuario DROP CONSTRAINT IF EXISTS CK_Usuario_Rol;
                ALTER TABLE dbo.Usuario DROP CONSTRAINT IF EXISTS DF_Usuario_Rol;
                ALTER TABLE dbo.Usuario DROP COLUMN IF EXISTS Rol;

                DROP INDEX IF EXISTS IX_PedidoDetalle_IDPedido ON dbo.PedidoDetalle;
                ALTER TABLE dbo.PedidoDetalle DROP CONSTRAINT IF EXISTS DF_PedidoDetalle_Eliminado;
                ALTER TABLE dbo.PedidoDetalle ALTER COLUMN Eliminado bit NULL;

                DROP INDEX IF EXISTS IX_Pedido_IDCliente ON dbo.Pedido;
                DROP INDEX IF EXISTS IX_Pedido_FechaPedido ON dbo.Pedido;
                DROP INDEX IF EXISTS UX_Pedido_NumeroPedido ON dbo.Pedido;
                ALTER TABLE dbo.Pedido DROP CONSTRAINT IF EXISTS CK_Pedido_Anulacion;
                ALTER TABLE dbo.Pedido DROP CONSTRAINT IF EXISTS CK_Pedido_TotalPedido;
                ALTER TABLE dbo.Pedido DROP CONSTRAINT IF EXISTS CK_Pedido_Estado;
                ALTER TABLE dbo.Pedido DROP CONSTRAINT IF EXISTS DF_Pedido_Estado;
                ALTER TABLE dbo.Pedido DROP COLUMN IF EXISTS Estado;
                ALTER TABLE dbo.Pedido DROP COLUMN IF EXISTS RowVersion;
                """);
        }
    }
}
