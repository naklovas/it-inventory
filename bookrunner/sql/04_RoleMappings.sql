/* ===========================================================================
   AD grubu -> uygulama rolu eslemesi

   BookRunner'da ayri bir kullanici/rol yonetimi yoktur; yetki tamamen Active
   Directory grup uyeliginden turetilir.

   NOT: Bu esleme, bir runbook'un SAHIBI ile ilgili degildir. Sahip, runbook'u
   kim olusturduysa odur ve kendi runbook'unda her degisikligi yapabilir.

   CALISTIRMADAN ONCE: asagidaki gruplari ve SID degerlerini kendi AD
   gruplarinizla degistirin. Grup adlarinin "BookRunner-" ile baslamasi
   gerekmez; kac grup eklerseniz ekleyin.

   Grup SID'ini ogrenmek icin (PowerShell):
       (Get-ADGroup 'Sunucu-Ekibi').SID.Value

   Roller: 0 = Viewer, 1 = Contributor, 2 = RunbookAuthor, 3 = Administrator

   DIKKAT: Viewer yalnizca okur. Gorev ATADIGINIZ gruplari en az Contributor
   olarak esleyin; aksi halde atanan kisi gorevi gorur ama uzerinde islem
   yapamaz (yorum, durum degisikligi, devir).
   =========================================================================== */

USE [BookRunner];
GO

/* >>> ASAGIDAKI TABLOYU DUZENLEYIN <<< */

DECLARE @mappings TABLE (GroupSid nvarchar(184), GroupName nvarchar(256), Role int);

INSERT INTO @mappings (GroupSid, GroupName, Role) VALUES
    (N'S-1-5-21-0000000000-0000000000-0000000000-1001', N'BookRunner-Administrators', 3),
    (N'S-1-5-21-0000000000-0000000000-0000000000-1002', N'BookRunner-Authors',        2),
    (N'S-1-5-21-0000000000-0000000000-0000000000-1003', N'BookRunner-Contributors',   1);

/* --------------------------- buradan asagisi degistirilmeden calisir ------ */

/* Ornek SID'ler sifir bloklariyla yazilmistir; duzenlenmeden calistirilirsa
   veritabanina anlamsiz esleme yazilmasin diye islem durdurulur. */
IF EXISTS (SELECT 1 FROM @mappings WHERE GroupSid LIKE N'S-1-5-21-0000000000-%')
BEGIN
    PRINT N'';
    PRINT N'-------------------------------------------------------------------';
    PRINT N'ATLANDI: SID degerleri hala ornek.';
    PRINT N'';
    PRINT N'Yukaridaki INSERT satirlarini kendi AD gruplarinizla degistirin.';
    PRINT N'Grup SID''i icin (PowerShell):';
    PRINT N'    (Get-ADGroup ''Sunucu-Ekibi'').SID.Value';
    PRINT N'';
    PRINT N'Alternatif: eslemeleri appsettings.json icindeki';
    PRINT N'Authorization:RoleMappings bolumunden de tanimlayabilirsiniz;';
    PRINT N'uygulama ilk acilista bunlari veritabanina yazar.';
    PRINT N'-------------------------------------------------------------------';
END
ELSE
BEGIN
    MERGE bookrunner.RoleMappings AS target
    USING @mappings AS source
    ON target.GroupSid = source.GroupSid AND target.Role = source.Role
    WHEN MATCHED THEN
        UPDATE SET GroupName = source.GroupName, IsActive = 1
    WHEN NOT MATCHED THEN
        INSERT (Id, GroupSid, GroupName, Role, IsActive, CreatedAt, CreatedBy)
        VALUES (NEWID(), source.GroupSid, source.GroupName, source.Role, 1, SYSDATETIMEOFFSET(), N'SETUP');

    PRINT N'Rol eslemeleri guncellendi.';

    SELECT GroupName, GroupSid, Role, IsActive
    FROM bookrunner.RoleMappings
    ORDER BY Role DESC, GroupName;
END
GO
