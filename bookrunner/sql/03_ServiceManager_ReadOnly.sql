/* ===========================================================================
   Service Manager'a veritabani seviyesinde SALT-OKUNUR erisim.

   BookRunner, SCSM konsolu veya SDK'si yerine dogrudan SQL ile okuma yapar.
   Bu script'i SCSM Data Warehouse (varsayilan: DWDataMart) sunucusunda,
   yonetici yetkisiyle calistirin.

   Onemli: Uygulama hesabina yalnizca SELECT verilir. BookRunner SCSM'e
   hicbir kosulda yazmaz.
   =========================================================================== */

USE [DWDataMart];
GO

DECLARE @appAccount sysname = N'CONTOSO\svc-bookrunner';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @appAccount)
BEGIN
    EXEC(N'CREATE LOGIN [' + @appAccount + N'] FROM WINDOWS');
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @appAccount)
BEGIN
    EXEC(N'CREATE USER [' + @appAccount + N'] FOR LOGIN [' + @appAccount + N']');
END

/* Tum veritabaninda okuma yerine yalnizca gerekli gorunumlere izin vermek
   isterseniz asagidaki db_datareader satirini kaldirip GRANT SELECT
   satirlarini kullanin. */
EXEC(N'ALTER ROLE [db_datareader] ADD MEMBER [' + @appAccount + N']');

/* Daha dar yetki tercih edilirse:
GRANT SELECT ON OBJECT::dbo.ChangeRequestDimvw TO [CONTOSO\svc-bookrunner];
GRANT SELECT ON OBJECT::dbo.ChangeStatusvw     TO [CONTOSO\svc-bookrunner];
GRANT SELECT ON OBJECT::dbo.ChangeAreavw       TO [CONTOSO\svc-bookrunner];
*/

/* Yazma yetkisi acikca reddedilir. */
EXEC(N'DENY INSERT, UPDATE, DELETE, EXECUTE TO [' + @appAccount + N']');
GO

/* ---------------------------------------------------------------------------
   Dogrulama: appsettings.json icindeki ServiceManager:SearchQuery ile ayni
   sutunlarin geldigini kontrol edin. Ortaminizdaki gorunum adlari farkliysa
   sorguyu yapilandirmadan degistirebilirsiniz; kod degistirmek gerekmez.
   --------------------------------------------------------------------------- */
SELECT TOP (5)
    cr.Id, cr.Title, status.DisplayName AS [Status], cr.ScheduledStartDate
FROM dbo.ChangeRequestDimvw AS cr
LEFT JOIN dbo.ChangeStatusvw AS status ON status.ChangeStatusId = cr.Status_ChangeStatusId
ORDER BY cr.CreatedDate DESC;
GO
