/* ===========================================================================
   OPSIYONEL - Uygulama hesabina veritabani yetkisi

   BU DOSYA COGU DURUMDA GEREKMEZ.

   Gerekli oldugu tek durum: BookRunner'i, veritabaninda henuz yetkisi olmayan
   AYRI bir Windows hesabi altinda calistiracaksaniz. Ornegin:
     - IIS uygulama havuzu kimligi
     - Windows servisi oturum hesabi

   Uygulamayi sunucuda kendi hesabinizla calistiriyorsaniz ve o hesap zaten
   sysadmin veya db_owner ise bu dosyayi calistirmayin.

   Calistirma:  sqlcmd -S <sunucu> -d BookRunner -i 05_AppAccount_Optional.sql
   =========================================================================== */

USE [BookRunner];
GO

/* Uygulamanin calisacagi Windows hesabini yazin, orn. N'CONTOSO\svc-bookrunner'
   veya N'IIS APPPOOL\BookRunner'. Bos birakilirsa script hicbir sey yapmaz. */
DECLARE @appAccount sysname = N'';

/* --------------------------- buradan asagisi degistirilmeden calisir ------ */

DECLARE @sql nvarchar(max);

IF @appAccount = N''
BEGIN
    PRINT N'@appAccount bos; islem yapilmadi.';
END
ELSE
BEGIN
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @appAccount)
        BEGIN
            SET @sql = N'CREATE LOGIN ' + QUOTENAME(@appAccount) + N' FROM WINDOWS';
            EXEC sp_executesql @sql;
            PRINT N'Login olusturuldu: ' + @appAccount;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @appAccount)
        BEGIN
            SET @sql = N'CREATE USER ' + QUOTENAME(@appAccount) + N' FOR LOGIN ' + QUOTENAME(@appAccount);
            EXEC sp_executesql @sql;
            PRINT N'Veritabani kullanicisi olusturuldu.';
        END

        /* db_ddladmin yalnizca Database:MigrateOnStartup = true iken gereklidir;
           semayi elle yonetiyorsaniz o satiri kaldirabilirsiniz. */
        SET @sql =
            N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@appAccount) + N';' +
            N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@appAccount) + N';' +
            N'ALTER ROLE [db_ddladmin]   ADD MEMBER ' + QUOTENAME(@appAccount) + N';';
        EXEC sp_executesql @sql;

        PRINT N'Roller verildi: db_datareader, db_datawriter, db_ddladmin';
    END TRY
    BEGIN CATCH
        PRINT N'Basarisiz. Hata ' + CAST(ERROR_NUMBER() AS nvarchar(10)) + N': ' + ERROR_MESSAGE();

        IF ERROR_NUMBER() IN (15401, 15007)
        BEGIN
            PRINT N'"' + @appAccount + N'" hesabi bulunamadi. Hesap adini kontrol edin.';
        END
    END CATCH
END
GO
