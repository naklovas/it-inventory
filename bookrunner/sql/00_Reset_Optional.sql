/* ===========================================================================
   OPSIYONEL VE YIKICI - bookrunner semasini tamamen siler

   Bu script'i yalnizca YARIM KALMIS bir kurulumu temizleyip SIFIRDAN
   baslamak isterseniz calistirin. RoleMappings, Runbooks, Tasks - ne
   varsa hepsini kalici olarak siler.

   Cogu durumda buna GEREK YOKTUR: 02_BookRunner_Schema.sql zaten tekrar
   calistirilabilir sekilde yazildi - eksik kalani tamamlar, var olana
   dokunmaz. Once onu tekrar calistirmayi deneyin; sadece "bu tablolarin
   yapisi bozuk, temiz baslamak istiyorum" derseniz bu script'i kullanin.

   Calistirma:  sqlcmd -S <sunucu> -d BookRunner -i 00_Reset_Optional.sql
   =========================================================================== */

USE [BookRunner];
GO

IF SCHEMA_ID(N'bookrunner') IS NULL
BEGIN
    PRINT N'bookrunner semasi yok; silinecek bir sey bulunamadi.';
    RETURN;
END
GO

/* Once tum foreign key'ler kaldirilir; boylece tablolar hangi sirada
   silinirse silinsin bir bagimlilik hatasi alinmaz. */
DECLARE @sql nvarchar(max) = N'';

SELECT @sql = @sql + N'ALTER TABLE [bookrunner].[' + OBJECT_NAME(parent_object_id) +
              N'] DROP CONSTRAINT [' + name + N'];' + CHAR(10)
FROM sys.foreign_keys
WHERE SCHEMA_NAME(schema_id) = N'bookrunner';

IF LEN(@sql) > 0
BEGIN
    EXEC sp_executesql @sql;
    PRINT N'Tum iliskiler (foreign key) kaldirildi.';
END
GO

/* Simdi tablolar sirasiz silinebilir. */
DECLARE @dropSql nvarchar(max) = N'';

SELECT @dropSql = @dropSql + N'DROP TABLE [bookrunner].[' + name + N'];' + CHAR(10)
FROM sys.tables
WHERE SCHEMA_NAME(schema_id) = N'bookrunner';

IF LEN(@dropSql) > 0
BEGIN
    EXEC sp_executesql @dropSql;
    PRINT N'Tum tablolar silindi.';
END
GO

DROP SCHEMA IF EXISTS [bookrunner];
GO

PRINT N'';
PRINT N'bookrunner semasi tamamen silindi. Simdi bastan baslayabilirsiniz:';
PRINT N'  sqlcmd -S <sunucu> -d BookRunner -i 02_BookRunner_Schema.sql';
GO
