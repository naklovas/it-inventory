/* ===========================================================================
   BookRunner - veritabani ve uygulama hesabi olusturma.
   SQL Server Management Studio'da yonetici yetkisiyle calistirin.
   =========================================================================== */

IF DB_ID(N'BookRunner') IS NULL
BEGIN
    CREATE DATABASE [BookRunner];
END
GO

ALTER DATABASE [BookRunner] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

USE [BookRunner];
GO

/* Uygulama semasi. EF Core migration'lari bu sema altinda calisir. */
IF SCHEMA_ID(N'bookrunner') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [bookrunner]');
END
GO

/* ---------------------------------------------------------------------------
   Uygulama havuzu kimligi. IIS/Windows servisi hangi hesapla calisiyorsa
   asagidaki adi ona gore degistirin (orn. CONTOSO\svc-bookrunner$).
   --------------------------------------------------------------------------- */
DECLARE @appAccount sysname = N'CONTOSO\svc-bookrunner';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @appAccount)
BEGIN
    EXEC(N'CREATE LOGIN [' + @appAccount + N'] FROM WINDOWS');
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @appAccount)
BEGIN
    EXEC(N'CREATE USER [' + @appAccount + N'] FOR LOGIN [' + @appAccount + N']');
END

/* Uygulama kendi semasinda okuma/yazma yapar; migration icin ddl_admin gerekir.
   Migration'lari ayri bir dagitim hesabiyla calistiriyorsanuz db_ddladmin
   uyeligini uygulama hesabindan kaldirabilirsiniz. */
EXEC(N'ALTER ROLE [db_datareader] ADD MEMBER [' + @appAccount + N']');
EXEC(N'ALTER ROLE [db_datawriter] ADD MEMBER [' + @appAccount + N']');
EXEC(N'ALTER ROLE [db_ddladmin]   ADD MEMBER [' + @appAccount + N']');
GO

PRINT N'BookRunner veritabani hazir. Simdi 02_BookRunner_Schema.sql dosyasini calistirin.';
GO
