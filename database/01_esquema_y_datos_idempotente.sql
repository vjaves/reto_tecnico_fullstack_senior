 
GO

BEGIN TRANSACTION;
 
    EXEC(N'IF OBJECT_ID(N''dbo.Empresa'', N''U'') IS NULL
    CREATE TABLE dbo.Empresa (
        IDEmpresa int NOT NULL CONSTRAINT PK_Empresa PRIMARY KEY,
        Ruc varchar(20) NOT NULL,
        RazonSocial varchar(200) NOT NULL,
        NombreComercial varchar(100) NULL,
        Direccion varchar(150) NULL,
        Estado bit NULL,
        IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
        IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.Sucursal'', N''U'') IS NULL
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
        CodigoEstablecimientoSunat varchar(4) NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.Moneda'', N''U'') IS NULL
    CREATE TABLE dbo.Moneda (
        IDMoneda int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Moneda PRIMARY KEY,
        Nombre varchar(100) NOT NULL,
        Abreviatura varchar(5) NULL,
        Estado bit NULL,
        IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
        IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.UnidadMedida'', N''U'') IS NULL
    CREATE TABLE dbo.UnidadMedida (
        IDUnidadMedida int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UnidadMedida PRIMARY KEY,
        Codigo varchar(3) NOT NULL,
        Nombre varchar(100) NOT NULL,
        Abreviatura varchar(5) NULL,
        CodigoSunat varchar(10) NULL,
        Estado bit NULL,
        IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
        IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.Producto'', N''U'') IS NULL
    CREATE TABLE dbo.Producto (
        IDProducto int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Producto PRIMARY KEY,
        IDEmpresa int NULL,
        IDUnidadMedida int NULL CONSTRAINT FK_Producto_UnidadMedida REFERENCES dbo.UnidadMedida (IDUnidadMedida),
        CodigoProducto varchar(50) NULL,
        Nombre varchar(1000) NOT NULL,
        Descripcion varchar(8000) NULL,
        Estado bit NOT NULL,
        IDUsuarioCreacion int NOT NULL, FechaCreacion datetime NOT NULL,
        IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.TipoDocumento'', N''U'') IS NULL
    CREATE TABLE dbo.TipoDocumento (
        IDTipoDocumento int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TipoDocumento PRIMARY KEY,
        Nombre varchar(100) NOT NULL,
        Abreviatura varchar(5) NULL,
        Estado bit NULL,
        IDUsuarioCreacion int NULL, FechaCreacion datetime NULL,
        IDUsuarioModificacion int NULL, FechaModificacion datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.Cliente'', N''U'') IS NULL
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
        IDUsuarioAnulacion int NULL, FechaAnulacion datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.Pedido'', N''U'') IS NULL
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
        IDUsuarioAnulado int NULL, FechaAnulado datetime NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.PedidoDetalle'', N''U'') IS NULL
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
        Eliminado bit NULL);');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.Usuario'', N''U'') IS NULL
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
        Eliminado bit NULL);');
GO

    EXEC(N'IF COL_LENGTH(N''dbo.Pedido'', N''Estado'') IS NULL
    ALTER TABLE dbo.Pedido ADD Estado varchar(20) NOT NULL
        CONSTRAINT DF_Pedido_Estado DEFAULT (''Registrado'');');
GO

    EXEC(N'IF COL_LENGTH(N''dbo.Pedido'', N''RowVersion'') IS NULL
    ALTER TABLE dbo.Pedido ADD RowVersion rowversion NOT NULL;');
GO

    EXEC(N'UPDATE dbo.Pedido SET Estado = ''Anulado'' WHERE FechaAnulado IS NOT NULL AND Estado <> ''Anulado'';');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.CK_Pedido_Estado'', N''C'') IS NULL
    ALTER TABLE dbo.Pedido ADD CONSTRAINT CK_Pedido_Estado
        CHECK ([Estado] IN (''Registrado'',''Confirmado'',''Despachado'',''Entregado'',''Anulado''));

    IF OBJECT_ID(N''dbo.CK_Pedido_TotalPedido'', N''C'') IS NULL
    ALTER TABLE dbo.Pedido ADD CONSTRAINT CK_Pedido_TotalPedido CHECK ([TotalPedido] > 0);

    IF OBJECT_ID(N''dbo.CK_Pedido_Anulacion'', N''C'') IS NULL
    ALTER TABLE dbo.Pedido ADD CONSTRAINT CK_Pedido_Anulacion
        CHECK (([Estado] = ''Anulado'' AND [FechaAnulado] IS NOT NULL)
            OR ([Estado] <> ''Anulado'' AND [FechaAnulado] IS NULL));

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''UX_Pedido_NumeroPedido'' AND object_id = OBJECT_ID(N''dbo.Pedido''))
    CREATE UNIQUE INDEX UX_Pedido_NumeroPedido ON dbo.Pedido (NumeroPedido);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_Pedido_FechaPedido'' AND object_id = OBJECT_ID(N''dbo.Pedido''))
    CREATE INDEX IX_Pedido_FechaPedido ON dbo.Pedido (FechaPedido);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_Pedido_IDCliente'' AND object_id = OBJECT_ID(N''dbo.Pedido''))
    CREATE INDEX IX_Pedido_IDCliente ON dbo.Pedido (IDCliente);');
GO

    EXEC(N'UPDATE dbo.PedidoDetalle SET Eliminado = 0 WHERE Eliminado IS NULL;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N''dbo.PedidoDetalle'') AND name = N''Eliminado'' AND is_nullable = 1)
    ALTER TABLE dbo.PedidoDetalle ALTER COLUMN Eliminado bit NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N''dbo.PedidoDetalle'')
                   AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N''dbo.PedidoDetalle''), N''Eliminado'', ''ColumnId''))
    ALTER TABLE dbo.PedidoDetalle ADD CONSTRAINT DF_PedidoDetalle_Eliminado DEFAULT (0) FOR Eliminado;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_PedidoDetalle_IDPedido'' AND object_id = OBJECT_ID(N''dbo.PedidoDetalle''))
    CREATE INDEX IX_PedidoDetalle_IDPedido ON dbo.PedidoDetalle (IDPedido);');
GO

    EXEC(N'IF COL_LENGTH(N''dbo.Usuario'', N''Rol'') IS NULL
    ALTER TABLE dbo.Usuario ADD Rol varchar(20) NOT NULL
        CONSTRAINT DF_Usuario_Rol DEFAULT (''User'');');
GO

    EXEC(N'IF OBJECT_ID(N''dbo.CK_Usuario_Rol'', N''C'') IS NULL
    ALTER TABLE dbo.Usuario ADD CONSTRAINT CK_Usuario_Rol CHECK ([Rol] IN (''Admin'',''User''));

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''UX_Usuario_Email'' AND object_id = OBJECT_ID(N''dbo.Usuario''))
    CREATE UNIQUE INDEX UX_Usuario_Email ON dbo.Usuario (Email);');
GO

    EXEC(N'INSERT dbo.Empresa (IDEmpresa, Ruc, RazonSocial, NombreComercial, Direccion, Estado, IDUsuarioCreacion, FechaCreacion)
    SELECT 1, ''20601234567'', ''ATLANTIC DISTRIBUCIONES S.A.C.'', ''Atlantic'', ''Av. Javier Prado Este 1234, San Isidro, Lima'', 1, 1, GETDATE()
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Empresa WHERE IDEmpresa = 1);

    INSERT dbo.Sucursal (IDSucursal, IDEmpresa, Nombre, Direccion, Estado, EsOficina, IDUsuarioCreacion, FechaCreacion)
    SELECT v.IDSucursal, 1, v.Nombre, v.Direccion, 1, 1, 1, GETDATE()
    FROM (VALUES (1, ''Sede Central - Lima'', ''Av. Javier Prado Este 1234, San Isidro''),
                 (2, ''Sucursal Arequipa'', ''Calle Mercaderes 215, Cercado de Arequipa'')) v(IDSucursal, Nombre, Direccion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Sucursal s WHERE s.IDSucursal = v.IDSucursal);');
GO

    EXEC(N'SET IDENTITY_INSERT dbo.Moneda ON;
    INSERT dbo.Moneda (IDMoneda, Nombre, Abreviatura, Estado, IDUsuarioCreacion, FechaCreacion)
    SELECT * FROM (VALUES (1, ''Soles'', ''PEN'', 1, 1, GETDATE()),
    (2, ''Dólares Americanos'', ''USD'', 1, 1, GETDATE())) v(IDMoneda,Nombre,Abreviatura,Estado,IDUsuarioCreacion,FechaCreacion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Moneda t WHERE t.IDMoneda = v.IDMoneda);
    SET IDENTITY_INSERT dbo.Moneda OFF;');
GO

    EXEC(N'SET IDENTITY_INSERT dbo.TipoDocumento ON;
    INSERT dbo.TipoDocumento (IDTipoDocumento, Nombre, Abreviatura, Estado, IDUsuarioCreacion, FechaCreacion)
    SELECT * FROM (VALUES (1, ''Documento Nacional de Identidad'', ''DNI'', 1, 1, GETDATE()),
    (2, ''Registro Único de Contribuyentes'', ''RUC'', 1, 1, GETDATE())) v(IDTipoDocumento,Nombre,Abreviatura,Estado,IDUsuarioCreacion,FechaCreacion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TipoDocumento t WHERE t.IDTipoDocumento = v.IDTipoDocumento);
    SET IDENTITY_INSERT dbo.TipoDocumento OFF;');
GO

    EXEC(N'SET IDENTITY_INSERT dbo.UnidadMedida ON;
    INSERT dbo.UnidadMedida (IDUnidadMedida, Codigo, Nombre, Abreviatura, CodigoSunat, Estado, IDUsuarioCreacion, FechaCreacion)
    SELECT * FROM (VALUES (1, ''NIU'', ''Unidad'', ''UND'', ''NIU'', 1, 1, GETDATE()),
    (2, ''BX'', ''Caja'', ''CJA'', ''BX'', 1, 1, GETDATE())) v(IDUnidadMedida,Codigo,Nombre,Abreviatura,CodigoSunat,Estado,IDUsuarioCreacion,FechaCreacion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.UnidadMedida t WHERE t.IDUnidadMedida = v.IDUnidadMedida);
    SET IDENTITY_INSERT dbo.UnidadMedida OFF;');
GO

    EXEC(N'SET IDENTITY_INSERT dbo.Producto ON;
    INSERT dbo.Producto (IDProducto, IDEmpresa, IDUnidadMedida, CodigoProducto, Nombre, Estado, IDUsuarioCreacion, FechaCreacion)
    SELECT * FROM (VALUES (1, 1, 1, ''P001'', ''Laptop Lenovo ThinkPad E14 Gen 5'', 1, 1, GETDATE()),
    (2, 1, 1, ''P002'', ''Monitor LG 27" UltraFine 4K'', 1, 1, GETDATE()),
    (3, 1, 1, ''P003'', ''Teclado mecánico Logitech MX Mechanical'', 1, 1, GETDATE()),
    (4, 1, 1, ''P004'', ''Mouse inalámbrico Logitech MX Master 3S'', 1, 1, GETDATE()),
    (5, 1, 1, ''P005'', ''SSD Kingston NV2 1TB NVMe'', 1, 1, GETDATE()),
    (6, 1, 1, ''P006'', ''Memoria RAM Kingston Fury 16GB DDR5'', 1, 1, GETDATE()),
    (7, 1, 1, ''P007'', ''Impresora multifuncional Epson EcoTank L3250'', 1, 1, GETDATE()),
    (8, 1, 2, ''P008'', ''Papel bond A4 75g (caja x 5 millares)'', 1, 1, GETDATE()),
    (9, 1, 1, ''P009'', ''Router TP-Link Archer AX55 WiFi 6'', 1, 1, GETDATE()),
    (10, 1, 1, ''P010'', ''Silla ergonómica Ergo Pro'', 1, 1, GETDATE())) v(IDProducto,IDEmpresa,IDUnidadMedida,CodigoProducto,Nombre,Estado,IDUsuarioCreacion,FechaCreacion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Producto t WHERE t.IDProducto = v.IDProducto);
    SET IDENTITY_INSERT dbo.Producto OFF;');
GO

    EXEC(N'SET IDENTITY_INSERT dbo.Cliente ON;
    INSERT dbo.Cliente (IDCliente, IDEmpresa, IDTipoDocumento, NumeroDocumento, PrimerNombre, ApellidoPaterno, ApellidoMaterno, RazonSocial, Estado, Eliminado, IDUsuarioCreacion, FechaCreacion)
    SELECT * FROM (VALUES (1, 1, 1, ''45781236'', ''Juan'', ''Perez'', NULL, NULL, 1, 0, 1, GETDATE()),
    (2, 1, 1, ''70214589'', ''María'', ''Quispe'', ''Huamán'', NULL, 1, 0, 1, GETDATE()),
    (3, 1, 1, ''41236587'', ''Carlos'', ''Rodríguez'', ''Salas'', NULL, 1, 0, 1, GETDATE()),
    (4, 1, 1, ''46598712'', ''Lucía'', ''Torres'', ''Mendoza'', NULL, 1, 0, 1, GETDATE()),
    (5, 1, 2, ''20512345678'', NULL, NULL, NULL, ''INVERSIONES ANDINAS S.A.C.'', 1, 0, 1, GETDATE()),
    (6, 1, 2, ''20456789123'', NULL, NULL, NULL, ''CORPORACIÓN PACÍFICO DEL SUR S.A.'', 1, 0, 1, GETDATE()),
    (7, 1, 2, ''20601122334'', NULL, NULL, NULL, ''TECNOLOGÍA Y SERVICIOS LIMA E.I.R.L.'', 1, 0, 1, GETDATE()),
    (8, 1, 2, ''20100234567'', NULL, NULL, NULL, ''COMERCIAL SAN MARTÍN S.R.L.'', 1, 0, 1, GETDATE())) v(IDCliente,IDEmpresa,IDTipoDocumento,NumeroDocumento,PrimerNombre,ApellidoPaterno,ApellidoMaterno,RazonSocial,Estado,Eliminado,IDUsuarioCreacion,FechaCreacion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Cliente t WHERE t.IDCliente = v.IDCliente);
    SET IDENTITY_INSERT dbo.Cliente OFF;');
GO

    EXEC(N'INSERT dbo.Usuario (IDUsuario, IDEmpresa, Usuario, Clave, NumeroDocumento, NombreCompleto, Email, Rol,
                        Bloqueado, NumIntentoFallidoLogin, CambiarClave, Eliminado, IDUsuarioCreacion, FechaCreacion)
    SELECT v.IDUsuario, 1, v.Usuario, v.Clave, v.Doc, v.Nombre, v.Email, v.Rol, 0, 0, 0, 0, 1, GETDATE()
    FROM (VALUES
        (1, ''admin'', ''$2a$11$MayRe1GDc7Y5HhvaLq/AA.VL4z7k1gh447GME6yspCj.K2N7EwqM.'', ''10000001'', ''Administrador del Sistema'', ''admin@email.com'', ''Admin''),
        (2, ''user'',  ''$2a$11$RomfsP9FFM7PRRHf2GQkiOICSOv.p0MErQy9SLmeSsHsyQdm/iiLG'', ''10000002'', ''Usuario Operador'', ''user@email.com'', ''User'')
    ) v(IDUsuario, Usuario, Clave, Doc, Nombre, Email, Rol)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Usuario u WHERE u.IDUsuario = v.IDUsuario OR u.Email = v.Email);');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-001'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (1, 1, 1, ''PED-001'', ''20250110'',
                0, 250.75, ''Registrado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 4, 1, 1, 250.75, 0, 250.75, 250.75, 1, GETDATE(), 0);
    END');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-002'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (1, 5, 1, ''PED-002'', ''20260901'',
                100, 8199.50, ''Confirmado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 1, 1, 2, 3899.00, 100, 7798.00, 7698.00, 1, GETDATE(), 0),
    (@id, 4, 2, 2, 250.75, 0, 501.50, 501.50, 1, GETDATE(), 0);
    END');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-003'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (1, 2, 1, ''PED-003'', ''20260910'',
                0, 1948.90, ''Despachado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 2, 1, 1, 1459.90, 0, 1459.90, 1459.90, 1, GETDATE(), 0),
    (@id, 3, 2, 1, 489.00, 0, 489.00, 489.00, 1, GETDATE(), 0);
    END');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-004'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (1, 6, 2, ''PED-004'', ''20260915'',
                0, 1042.50, ''Entregado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 9, 1, 5, 129.00, 0, 645.00, 645.00, 1, GETDATE(), 0),
    (@id, 5, 2, 5, 79.50, 0, 397.50, 397.50, 1, GETDATE(), 0);
    END');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-005'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (1, 3, 1, ''PED-005'', ''20260920'',
                50, 1400.00, ''Registrado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 8, 1, 10, 145.00, 50, 1450.00, 1400.00, 1, GETDATE(), 0);
    END');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-006'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (2, 7, 1, ''PED-006'', ''20260922'',
                0, 4555.20, ''Registrado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 10, 1, 4, 699.00, 0, 2796.00, 2796.00, 1, GETDATE(), 0),
    (@id, 6, 2, 8, 219.90, 0, 1759.20, 1759.20, 1, GETDATE(), 0);
    END');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.Pedido WHERE NumeroPedido = ''PED-007'')
    BEGIN
        DECLARE @id int;
        INSERT dbo.Pedido (IDSucursal, IDCliente, IDMoneda, NumeroPedido, FechaPedido, TotalDescuentos, TotalPedido,
                           Estado, IDUsuarioCreacion, FechaCreacion)
        VALUES (2, 4, 1, ''PED-007'', ''20260923'',
                0, 849.00, ''Registrado'', 1, GETDATE());
        SET @id = SCOPE_IDENTITY();

        INSERT dbo.PedidoDetalle (IDPedido, IDProducto, Item, Cantidad, PrecioVenta, Descuento, SubTotal, ImporteTotal,
                                  IDUsuarioCreacion, FechaCreacion, Eliminado)
        VALUES (@id, 7, 1, 1, 849.00, 0, 849.00, 849.00, 1, GETDATE(), 0);
    END');
 
GO

    EXEC(N'IF COL_LENGTH(N''dbo.Producto'', N''Eliminado'') IS NULL
    ALTER TABLE dbo.Producto ADD Eliminado bit NOT NULL
        CONSTRAINT DF_Producto_Eliminado DEFAULT (0);');
GO

    EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_Cliente_Empresa_Documento'' AND object_id = OBJECT_ID(N''dbo.Cliente''))
    CREATE INDEX IX_Cliente_Empresa_Documento ON dbo.Cliente (IDEmpresa, NumeroDocumento);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_Producto_Empresa_Codigo'' AND object_id = OBJECT_ID(N''dbo.Producto''))
    CREATE INDEX IX_Producto_Empresa_Codigo ON dbo.Producto (IDEmpresa, CodigoProducto);');
GO
  
COMMIT;
GO

