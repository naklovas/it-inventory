/* ===========================================================================
   AD grubu -> uygulama rolu eslemesi.

   BookRunner'da ayri bir kullanici/rol yonetimi yoktur; yetki tamamen Active
   Directory grup uyeliginden turetilir. Eslemeleri appsettings.json icindeki
   "Authorization:RoleMappings" bolumunden ya da bu tablodan yonetebilirsiniz.

   Grubun SID degerini PowerShell ile ogrenebilirsiniz:
       (Get-ADGroup 'BookRunner-Administrators').SID.Value
   =========================================================================== */

USE [BookRunner];
GO

/* Roller: 0 = Viewer, 1 = Contributor, 2 = RunbookAuthor, 3 = Administrator */
MERGE bookrunner.RoleMappings AS target
USING (VALUES
    (N'S-1-5-21-0000000000-0000000000-0000000000-1001', N'BookRunner-Administrators', 3),
    (N'S-1-5-21-0000000000-0000000000-0000000000-1002', N'BookRunner-Authors',        2),
    (N'S-1-5-21-0000000000-0000000000-0000000000-1003', N'BookRunner-Contributors',   1),
    (N'S-1-5-21-0000000000-0000000000-0000000000-1004', N'BookRunner-Viewers',        0)
) AS source (GroupSid, GroupName, Role)
ON target.GroupSid = source.GroupSid AND target.Role = source.Role
WHEN MATCHED THEN
    UPDATE SET GroupName = source.GroupName, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (Id, GroupSid, GroupName, Role, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), source.GroupSid, source.GroupName, source.Role, 1, SYSDATETIMEOFFSET(), N'SETUP');
GO

SELECT GroupName, GroupSid, Role, IsActive FROM bookrunner.RoleMappings ORDER BY Role DESC;
GO
