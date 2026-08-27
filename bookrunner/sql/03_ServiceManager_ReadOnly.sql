/* ===========================================================================
   Service Manager'a SALT-OKUNUR erisim

   BookRunner, Service Manager'a SDK veya konsol uzerinden degil, dogrudan
   veritabanindan ve YALNIZCA OKUYARAK baglanir. Bu script uygulama hesabina
   o okuma yetkisini verir.

   ONEMLI - urun farki
   -------------------
   Asagidaki ornek sorgu Microsoft System Center Service Manager'in (SCSM)
   Data Warehouse semasina goredir. Farkli bir Service Manager urunu
   kullaniyorsaniz (orn. OpenText / Micro Focus Service Manager):

     - Yetkilendirme bolumu (BOLUM 2) aynen gecerlidir.
     - Dogrulama sorgusu (BOLUM 3) ve appsettings icindeki
       ServiceManager:SearchQuery / GetByIdQuery degerleri kendi
       tablolarinizla degistirilmelidir. Kod degistirmek gerekmez.

   CALISTIRMADAN ONCE: @appAccount ve gerekiyorsa veritabani adini duzenleyin.
   Gereken yetki: hedef veritabaninda sysadmin veya securityadmin
   =========================================================================== */

/* ---------------------------------------------------------------------------
   BOLUM 1 - Hedef veritabani

   SCSM Data Warehouse icin varsayilan ad DWDataMart'tir. Baska bir urun
   kullaniyorsaniz kendi veritabani adinizi yazin.
   --------------------------------------------------------------------------- */

USE [DWDataMart];   -- <<< GEREKIRSE DEGISTIRIN
GO

/* ---------------------------------------------------------------------------
   BOLUM 2 - Uygulama hesabina okuma yetkisi

   >>> ASAGIDAKI SATIRI DUZENLEYIN <<<
   --------------------------------------------------------------------------- */

DECLARE @appAccount sysname = N'CONTOSO\svc-bookrunner';   -- <<< DEGISTIRIN

/* --------------------------- buradan asagisi degistirilmeden calisir ------ */

DECLARE @placeholder sysname = N'CONTOSO\svc-bookrunner';
DECLARE @sql nvarchar(max);

IF @appAccount = @placeholder
BEGIN
    PRINT N'';
    PRINT N'-------------------------------------------------------------------';
    PRINT N'ATLANDI: @appAccount hala ornek deger.';
    PRINT N'Script''in basindaki @appAccount satirini, uygulamanin calistigi';
    PRINT N'Windows hesabiyla degistirip tekrar calistirin.';
    PRINT N'-------------------------------------------------------------------';
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

        /* Tum veritabaninda okuma. Daha dar bir yetki isterseniz bu satiri
           kaldirip yalnizca ihtiyac duyulan tablo/gorunumlere GRANT SELECT
           verebilirsiniz:
             GRANT SELECT ON OBJECT::dbo.<tablo> TO <hesap>; */
        SET @sql = N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@appAccount);
        EXEC sp_executesql @sql;
        PRINT N'db_datareader rolu verildi.';

        /* Yazma yetkisi acikca reddedilir: BookRunner Service Manager'a
           hicbir kosulda yazmaz. */
        SET @sql = N'DENY INSERT, UPDATE, DELETE, EXECUTE TO ' + QUOTENAME(@appAccount);
        EXEC sp_executesql @sql;
        PRINT N'Yazma yetkileri reddedildi (DENY INSERT/UPDATE/DELETE/EXECUTE).';
    END TRY
    BEGIN CATCH
        PRINT N'';
        PRINT N'-------------------------------------------------------------------';
        PRINT N'BASARISIZ. Hata ' + CAST(ERROR_NUMBER() AS nvarchar(10)) + N': ' + ERROR_MESSAGE();

        IF ERROR_NUMBER() IN (15401, 15007)
        BEGIN
            PRINT N'"' + @appAccount + N'" hesabi Active Directory''de bulunamadi.';
            PRINT N'NetBIOS adiyla yazin (CONTOSO\kullanici) ve hesabin var oldugunu';
            PRINT N'dogrulayin (PowerShell: Get-ADUser <hesap>).';
        END
        ELSE IF ERROR_NUMBER() IN (15247, 262, 300)
        BEGIN
            PRINT N'Yetki eksik. Login olusturmak icin sysadmin veya securityadmin';
            PRINT N'rolu gerekir; SQL yoneticinizden calistirmasini isteyin.';
        END

        PRINT N'-------------------------------------------------------------------';
    END CATCH
END
GO

/* ---------------------------------------------------------------------------
   BOLUM 3 - Dogrulama (Microsoft SCSM ornegi)

   Asagidaki sorgu, appsettings icindeki ServiceManager:SearchQuery ile ayni
   sutunlari dondurmelidir:
     Id, Title, Description, Status, Category, AssignedTo,
     CreatedBy, CreatedDate, ScheduledStartDate, ScheduledEndDate, WorkItemType

   Farkli bir Service Manager urunu kullaniyorsaniz bu sorguyu kendi
   tablolarinizla degistirin; ayni sutun adlarini uretmeniz yeterlidir.
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.ChangeRequestDimvw') IS NULL
BEGIN
    PRINT N'';
    PRINT N'Not: dbo.ChangeRequestDimvw bulunamadi.';
    PRINT N'Bu veritabani Microsoft SCSM Data Warehouse degil gorunuyor.';
    PRINT N'Dogrulama sorgusu atlandi - yetkilendirme yine de yapildi.';
    PRINT N'appsettings icindeki ServiceManager sorgularini kendi tablolarinizla';
    PRINT N'degistirmeyi unutmayin.';
END
ELSE
BEGIN
    SELECT TOP (5)
        cr.Id, cr.Title, status.DisplayName AS [Status], cr.ScheduledStartDate
    FROM dbo.ChangeRequestDimvw AS cr
    LEFT JOIN dbo.ChangeStatusvw AS status ON status.ChangeStatusId = cr.Status_ChangeStatusId
    ORDER BY cr.CreatedDate DESC;
END
GO
