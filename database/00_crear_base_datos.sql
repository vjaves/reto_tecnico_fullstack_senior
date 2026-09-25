/*
  Paso opcional: crea la base si no existe.
  La API la necesita creada (el login de Windows debe poder conectarse); desde ahí,
  las migraciones de EF Core crean o completan el esquema al iniciar.

  sqlcmd -S "DESKTOP-0V9BORH\SQLEXPRESS" -E -C -I -f 65001 -i 00_crear_base_datos.sql
*/
IF DB_ID(N'BDExamenAtlantic') IS NULL
BEGIN
    CREATE DATABASE BDExamenAtlantic COLLATE Modern_Spanish_CI_AS;
    PRINT 'Base BDExamenAtlantic creada.';
END
ELSE
    PRINT 'La base BDExamenAtlantic ya existe.';
GO
