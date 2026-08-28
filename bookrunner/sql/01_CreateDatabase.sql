/* ===========================================================================
   BookRunner - veritabani

   Veritabanini zaten olusturduysaniz BU DOSYAYA IHTIYACINIZ YOK.
   Dogrudan 02_BookRunner_Schema.sql dosyasini calistirin.

   Calistirma:  sqlcmd -S <sunucu> -i 01_CreateDatabase.sql
   =========================================================================== */

IF DB_ID(N'BookRunner') IS NULL
BEGIN
    PRINT N'BookRunner veritabani olusturuluyor...';
    CREATE DATABASE [BookRunner];
END
ELSE
BEGIN
    PRINT N'BookRunner veritabani zaten var.';
END
GO

/* Okuma sorgulari yazma islemlerini beklemesin diye anlik goruntu izolasyonu.
   Yetki yetmezse kurulum durmaz; yalnizca uyari verilir. */
BEGIN TRY
    ALTER DATABASE [BookRunner] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
    PRINT N'READ_COMMITTED_SNAPSHOT acildi.';
END TRY
BEGIN CATCH
    PRINT N'UYARI: READ_COMMITTED_SNAPSHOT acilamadi -> ' + ERROR_MESSAGE();
END CATCH
GO

PRINT N'';
PRINT N'Sonraki adim - tablolari olusturun:';
PRINT N'  sqlcmd -S <sunucu> -d BookRunner -i 02_BookRunner_Schema.sql';
GO
