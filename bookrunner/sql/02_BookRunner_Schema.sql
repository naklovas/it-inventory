/* ===========================================================================
   BookRunner - tablolar, indeksler ve iliskiler

   Bu script BAGIMSIZ ve TEKRAR CALISTIRILABILIRDIR (idempotent):
     - Her tablo, her indeks ve her iliski (foreign key) KENDI BASINA kontrol
       edilir ve yalnizca yoksa olusturulur. Zaten varsa dokunulmaz.
     - Bir iliski herhangi bir nedenle olusturulamazsa (orn. veri tutarsizligi,
       gecici bir hata) script DURMAZ; uyari basar ve digerlerine devam eder.
     - Bu yuzden calistirirken bir hata gorseniz bile script'i TEKRAR
       calistirmak guvenlidir: eksik kalan neyse yalniz onu tamamlar.

   Onceki surumden fark: EF Core'un urettigi "idempotent" script yalnizca
   "bu migration calisti mi" diye tek bir bayraga bakiyordu; bir nesne
   olusturulamasa bile script sonuna kadar akip migration'i "tamamlandi"
   olarak isaretliyordu. Bu da tam olarak sizin yasadiginiz duruma yol
   aciyordu: bazi tablolar olustu, biri hata verdi, script "bitti" dedi ama
   şema eksik kaldi ve bir daha calistirilamadi. Simdi her nesne kendi
   basina kontrol edildigi icin boyle bir kilitlenme olmaz.

   Onceden veritabanini olusturmadiysaniz once 01_CreateDatabase.sql'i
   calistirin. Zaten olusturduysaniz dogrudan bu dosyayi calistirin:

     sqlcmd -S <sunucu> -d BookRunner -i 02_BookRunner_Schema.sql

   NetBIOS/hesap/domain ile ilgili hicbir sey icermez; yalnizca sema.
   =========================================================================== */

SET NOCOUNT ON;
GO

IF SCHEMA_ID(N'bookrunner') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [bookrunner];');
    PRINT N'Sema olusturuldu: bookrunner';
END
GO

/* ---------------------------------------------------------------------------
   Migration gecmisi

   Uygulama (appsettings > Database:MigrateOnStartup = true iken) acilista
   EF Core migration'larini kendisi uygular ve bunu bu tabloya kaydeder. Bu
   script tablolari elle olusturdugu icin, en sonda ayni kaydi biz yaziyoruz;
   boylece uygulama "bu migration zaten calisti" bilip tekrar denemez.
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'[bookrunner].[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT N'Tablo olusturuldu: __EFMigrationsHistory';
END
GO

/* ---------------------------------------------------------------------------
   Tablolar

   Iliskiler (foreign key) burada DEGIL, dosyanin sonunda ayri ayri eklenir.
   Boylece tablo olusturma sirasi onemsizdir ve bir iliskinin basarisiz
   olmasi tablo olusturmayi hic etkilemez.
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'[bookrunner].[AuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [Timestamp] datetimeoffset NOT NULL,
        [UserName] nvarchar(256) NOT NULL,
        [UserDisplayName] nvarchar(256) NULL,
        [Action] int NOT NULL,
        [EntityType] nvarchar(128) NOT NULL,
        [EntityId] nvarchar(64) NULL,
        [RunbookId] uniqueidentifier NULL,
        [Changes] nvarchar(max) NULL,
        [Summary] nvarchar(1000) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(512) NULL,
        [CorrelationId] nvarchar(64) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: AuditLogs';
END
GO

IF OBJECT_ID(N'[bookrunner].[EmailOutbox]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[EmailOutbox] (
        [Id] bigint NOT NULL IDENTITY,
        [To] nvarchar(2000) NOT NULL,
        [Cc] nvarchar(2000) NULL,
        [Subject] nvarchar(500) NOT NULL,
        [HtmlBody] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [AttemptCount] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [SentAt] datetimeoffset NULL,
        [NextAttemptAt] datetimeoffset NULL,
        [LastError] nvarchar(2000) NULL,
        [Reason] nvarchar(100) NULL,
        [RunbookId] uniqueidentifier NULL,
        [TaskId] uniqueidentifier NULL,
        CONSTRAINT [PK_EmailOutbox] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: EmailOutbox';
END
GO

IF OBJECT_ID(N'[bookrunner].[Groups]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[Groups] (
        [Id] uniqueidentifier NOT NULL,
        [Sid] nvarchar(184) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Email] nvarchar(256) NULL,
        [DistinguishedName] nvarchar(512) NULL,
        [AvatarColor] nvarchar(9) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsTeam] bit NOT NULL CONSTRAINT [DF_Groups_IsTeam] DEFAULT (0),
        [LastSyncedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Groups] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: Groups';
END
GO

-- Mevcut kurulumlarda Groups tablosu IsTeam olmadan olusturulmus olabilir; personel
-- servisinden gelen takim adlarindan turetilmis sanal gruplari ayirt etmek icin eklenir.
IF COL_LENGTH(N'bookrunner.Groups', 'IsTeam') IS NULL
BEGIN
    ALTER TABLE [bookrunner].[Groups] ADD [IsTeam] bit NOT NULL CONSTRAINT [DF_Groups_IsTeam] DEFAULT (0);
END
GO

IF OBJECT_ID(N'[bookrunner].[RoleMappings]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[RoleMappings] (
        [Id] uniqueidentifier NOT NULL,
        [TeamName] nvarchar(256) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_RoleMappings] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: RoleMappings';
END
GO

-- Daha once GroupSid/GroupName ile olusturulmus bir RoleMappings tablosu varsa
-- (AD grubu -> rol eslemesi), takim adi -> rol eslemesine gecirilir.
IF COL_LENGTH(N'bookrunner.RoleMappings', 'GroupSid') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'bookrunner.RoleMappings', 'TeamName') IS NULL
    BEGIN
        ALTER TABLE [bookrunner].[RoleMappings] ADD [TeamName] nvarchar(256) NULL;
        PRINT N'Kolon eklendi: RoleMappings.TeamName';
    END

    UPDATE [bookrunner].[RoleMappings] SET [TeamName] = [GroupName] WHERE [TeamName] IS NULL;

    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RoleMappings_GroupSid_Role' AND object_id = OBJECT_ID(N'[bookrunner].[RoleMappings]')
    )
    BEGIN
        DROP INDEX [IX_RoleMappings_GroupSid_Role] ON [bookrunner].[RoleMappings];
        PRINT N'Indeks kaldirildi: IX_RoleMappings_GroupSid_Role';
    END

    ALTER TABLE [bookrunner].[RoleMappings] ALTER COLUMN [TeamName] nvarchar(256) NOT NULL;
    ALTER TABLE [bookrunner].[RoleMappings] DROP COLUMN [GroupSid];
    ALTER TABLE [bookrunner].[RoleMappings] DROP COLUMN [GroupName];
    PRINT N'RoleMappings takim adina gore eslemeye tasindi (GroupSid/GroupName kaldirildi).';
END
GO

IF OBJECT_ID(N'[bookrunner].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [Sid] nvarchar(184) NOT NULL,
        [SamAccountName] nvarchar(256) NOT NULL,
        [UserPrincipalName] nvarchar(256) NULL,
        [DisplayName] nvarchar(256) NOT NULL,
        [Email] nvarchar(256) NULL,
        [Title] nvarchar(128) NULL,
        [Department] nvarchar(128) NULL,
        [Company] nvarchar(128) NULL,
        [OfficePhone] nvarchar(64) NULL,
        [MobilePhone] nvarchar(64) NULL,
        [ManagerDistinguishedName] nvarchar(512) NULL,
        [DistinguishedName] nvarchar(512) NULL,
        [Photo] varbinary(max) NULL,
        [PhotoContentType] nvarchar(64) NULL,
        [PhotoHash] nvarchar(64) NULL,
        [Initials] nvarchar(4) NOT NULL,
        [AvatarColor] nvarchar(9) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastSyncedAt] datetimeoffset NULL,
        [LastSeenAt] datetimeoffset NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: Users';
END
GO

IF OBJECT_ID(N'[bookrunner].[Runbooks]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[Runbooks] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(32) NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [IsTemplate] bit NOT NULL,
        [TemplateCategory] nvarchar(100) NULL,
        [ProgramName] nvarchar(150) NULL,
        [SourceTemplateId] uniqueidentifier NULL,
        [PlannedStart] datetimeoffset NULL,
        [PlannedEnd] datetimeoffset NULL,
        [ActualStart] datetimeoffset NULL,
        [ActualEnd] datetimeoffset NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [ServiceManagerWorkItemId] nvarchar(64) NULL,
        [Tags] nvarchar(1000) NULL,
        [RowVersion] rowversion NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(256) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_Runbooks] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: Runbooks';
END
GO

-- Mevcut kurulumlarda Runbooks tablosu ProgramName olmadan olusturulmus olabilir;
-- birden fazla runbook'u kapsayan ust baslik (Jira'daki "Epic" karsiligi) icin eklenir.
IF COL_LENGTH(N'bookrunner.Runbooks', 'ProgramName') IS NULL
BEGIN
    ALTER TABLE [bookrunner].[Runbooks] ADD [ProgramName] nvarchar(150) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_ProgramName' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE INDEX [IX_Runbooks_ProgramName] ON [bookrunner].[Runbooks] ([ProgramName]);
END
GO

IF OBJECT_ID(N'[bookrunner].[UserGroups]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[UserGroups] (
        [UserId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [SyncedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserGroups] PRIMARY KEY ([UserId], [GroupId])
    );
    PRINT N'Tablo olusturuldu: UserGroups';
END
GO

IF OBJECT_ID(N'[bookrunner].[Scripts]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[Scripts] (
        [Id] uniqueidentifier NOT NULL,
        [RunbookId] uniqueidentifier NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Code] nvarchar(max) NOT NULL,
        [TimeoutSeconds] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_Scripts] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: Scripts';
END
GO

IF OBJECT_ID(N'[bookrunner].[Tasks]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[Tasks] (
        [Id] uniqueidentifier NOT NULL,
        [RunbookId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ColorHex] nvarchar(9) NOT NULL,
        [Status] int NOT NULL,
        [Priority] int NOT NULL,
        [EstimatedMinutes] int NULL,
        [PlannedStart] datetimeoffset NULL,
        [PlannedEnd] datetimeoffset NULL,
        [ActualStart] datetimeoffset NULL,
        [ActualEnd] datetimeoffset NULL,
        [DependsOnTaskId] uniqueidentifier NULL,
        [ScriptId] uniqueidentifier NULL,
        [RollbackNotes] nvarchar(4000) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(256) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: Tasks';
END
GO

IF OBJECT_ID(N'[bookrunner].[ScriptExecutions]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[ScriptExecutions] (
        [Id] bigint NOT NULL IDENTITY,
        [ScriptId] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NULL,
        [ExecutedByUserId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [FinishedAt] datetimeoffset NULL,
        [DurationMs] bigint NOT NULL,
        [Result] nvarchar(max) NULL,
        [Output] nvarchar(max) NULL,
        [Error] nvarchar(max) NULL,
        CONSTRAINT [PK_ScriptExecutions] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: ScriptExecutions';
END
GO

IF OBJECT_ID(N'[bookrunner].[TaskActivities]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[TaskActivities] (
        [Id] bigint NOT NULL IDENTITY,
        [TaskId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorDisplayName] nvarchar(256) NOT NULL,
        [OldValue] nvarchar(512) NULL,
        [NewValue] nvarchar(512) NULL,
        [Summary] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TaskActivities] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: TaskActivities';
END
GO

IF OBJECT_ID(N'[bookrunner].[TaskAssignments]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[TaskAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [AssigneeType] int NOT NULL,
        [UserId] uniqueidentifier NULL,
        [GroupId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [HandedOverFromAssignmentId] uniqueidentifier NULL,
        [HandoverNote] nvarchar(1000) NULL,
        [ReleasedAt] datetimeoffset NULL,
        [NotifiedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_TaskAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TaskAssignments_Target] CHECK (([AssigneeType] = 0 AND [UserId] IS NOT NULL AND [GroupId] IS NULL) OR ([AssigneeType] = 1 AND [GroupId] IS NOT NULL AND [UserId] IS NULL))
    );
    PRINT N'Tablo olusturuldu: TaskAssignments';
END
GO

IF OBJECT_ID(N'[bookrunner].[TaskComments]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[TaskComments] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [AuthorUserId] uniqueidentifier NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [ParentCommentId] uniqueidentifier NULL,
        [MentionedUserIds] nvarchar(2000) NULL,
        [IsEdited] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(256) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: TaskComments';
END
GO

/* ---------------------------------------------------------------------------
   Oyunlastirma: puan, rozet katalogu, kazanilan rozetler.
   --------------------------------------------------------------------------- */

IF COL_LENGTH(N'bookrunner.Users', 'TeamName') IS NULL
BEGIN
    ALTER TABLE [bookrunner].[Users] ADD [TeamName] nvarchar(256) NULL;
    PRINT N'Kolon eklendi: Users.TeamName';
END
GO

IF OBJECT_ID(N'[bookrunner].[Badges]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[Badges] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Description] nvarchar(512) NOT NULL,
        [Icon] nvarchar(64) NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_Badges] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: Badges';
END
GO

IF OBJECT_ID(N'[bookrunner].[GamificationEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[GamificationEvents] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [EventType] int NOT NULL,
        [Points] int NOT NULL,
        [RunbookId] uniqueidentifier NULL,
        [RunbookTaskId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_GamificationEvents] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: GamificationEvents';
END
GO

IF OBJECT_ID(N'[bookrunner].[UserBadges]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[UserBadges] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [BadgeId] uniqueidentifier NOT NULL,
        [EarnedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserBadges] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: UserBadges';
END
GO

/* ---------------------------------------------------------------------------
   Runbook'a ozel "Editor" yetkisi: sahibin, kendi runbook'una global role
   dokunmadan ekledigi kisiler.
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'[bookrunner].[RunbookCollaborators]', N'U') IS NULL
BEGIN
    CREATE TABLE [bookrunner].[RunbookCollaborators] (
        [Id] uniqueidentifier NOT NULL,
        [RunbookId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [AddedAt] datetimeoffset NOT NULL,
        [AddedBy] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_RunbookCollaborators] PRIMARY KEY ([Id])
    );
    PRINT N'Tablo olusturuldu: RunbookCollaborators';
END
GO

/* ---------------------------------------------------------------------------
   Indeksler
   --------------------------------------------------------------------------- */

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLogs_EntityType_EntityId' AND object_id = OBJECT_ID(N'[bookrunner].[AuditLogs]')
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [bookrunner].[AuditLogs] ([EntityType], [EntityId]);
    PRINT N'Indeks olusturuldu: IX_AuditLogs_EntityType_EntityId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLogs_RunbookId' AND object_id = OBJECT_ID(N'[bookrunner].[AuditLogs]')
)
BEGIN
    CREATE INDEX [IX_AuditLogs_RunbookId] ON [bookrunner].[AuditLogs] ([RunbookId]);
    PRINT N'Indeks olusturuldu: IX_AuditLogs_RunbookId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLogs_Timestamp' AND object_id = OBJECT_ID(N'[bookrunner].[AuditLogs]')
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [bookrunner].[AuditLogs] ([Timestamp]);
    PRINT N'Indeks olusturuldu: IX_AuditLogs_Timestamp';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLogs_UserName' AND object_id = OBJECT_ID(N'[bookrunner].[AuditLogs]')
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserName] ON [bookrunner].[AuditLogs] ([UserName]);
    PRINT N'Indeks olusturuldu: IX_AuditLogs_UserName';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_EmailOutbox_Status_NextAttemptAt' AND object_id = OBJECT_ID(N'[bookrunner].[EmailOutbox]')
)
BEGIN
    CREATE INDEX [IX_EmailOutbox_Status_NextAttemptAt] ON [bookrunner].[EmailOutbox] ([Status], [NextAttemptAt]);
    PRINT N'Indeks olusturuldu: IX_EmailOutbox_Status_NextAttemptAt';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Groups_Name' AND object_id = OBJECT_ID(N'[bookrunner].[Groups]')
)
BEGIN
    CREATE INDEX [IX_Groups_Name] ON [bookrunner].[Groups] ([Name]);
    PRINT N'Indeks olusturuldu: IX_Groups_Name';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Groups_Sid' AND object_id = OBJECT_ID(N'[bookrunner].[Groups]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Groups_Sid] ON [bookrunner].[Groups] ([Sid]);
    PRINT N'Indeks olusturuldu: IX_Groups_Sid';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RoleMappings_TeamName_Role' AND object_id = OBJECT_ID(N'[bookrunner].[RoleMappings]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoleMappings_TeamName_Role] ON [bookrunner].[RoleMappings] ([TeamName], [Role]);
    PRINT N'Indeks olusturuldu: IX_RoleMappings_TeamName_Role';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_Code' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Runbooks_Code] ON [bookrunner].[Runbooks] ([Code]);
    PRINT N'Indeks olusturuldu: IX_Runbooks_Code';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_IsTemplate_Status' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE INDEX [IX_Runbooks_IsTemplate_Status] ON [bookrunner].[Runbooks] ([IsTemplate], [Status]);
    PRINT N'Indeks olusturuldu: IX_Runbooks_IsTemplate_Status';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_OwnerUserId' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE INDEX [IX_Runbooks_OwnerUserId] ON [bookrunner].[Runbooks] ([OwnerUserId]);
    PRINT N'Indeks olusturuldu: IX_Runbooks_OwnerUserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_PlannedStart' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE INDEX [IX_Runbooks_PlannedStart] ON [bookrunner].[Runbooks] ([PlannedStart]);
    PRINT N'Indeks olusturuldu: IX_Runbooks_PlannedStart';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_ServiceManagerWorkItemId' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE INDEX [IX_Runbooks_ServiceManagerWorkItemId] ON [bookrunner].[Runbooks] ([ServiceManagerWorkItemId]);
    PRINT N'Indeks olusturuldu: IX_Runbooks_ServiceManagerWorkItemId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Runbooks_SourceTemplateId' AND object_id = OBJECT_ID(N'[bookrunner].[Runbooks]')
)
BEGIN
    CREATE INDEX [IX_Runbooks_SourceTemplateId] ON [bookrunner].[Runbooks] ([SourceTemplateId]);
    PRINT N'Indeks olusturuldu: IX_Runbooks_SourceTemplateId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScriptExecutions_ExecutedByUserId' AND object_id = OBJECT_ID(N'[bookrunner].[ScriptExecutions]')
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_ExecutedByUserId] ON [bookrunner].[ScriptExecutions] ([ExecutedByUserId]);
    PRINT N'Indeks olusturuldu: IX_ScriptExecutions_ExecutedByUserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScriptExecutions_ScriptId' AND object_id = OBJECT_ID(N'[bookrunner].[ScriptExecutions]')
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_ScriptId] ON [bookrunner].[ScriptExecutions] ([ScriptId]);
    PRINT N'Indeks olusturuldu: IX_ScriptExecutions_ScriptId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScriptExecutions_StartedAt' AND object_id = OBJECT_ID(N'[bookrunner].[ScriptExecutions]')
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_StartedAt] ON [bookrunner].[ScriptExecutions] ([StartedAt]);
    PRINT N'Indeks olusturuldu: IX_ScriptExecutions_StartedAt';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScriptExecutions_TaskId' AND object_id = OBJECT_ID(N'[bookrunner].[ScriptExecutions]')
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_TaskId] ON [bookrunner].[ScriptExecutions] ([TaskId]);
    PRINT N'Indeks olusturuldu: IX_ScriptExecutions_TaskId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Scripts_Name' AND object_id = OBJECT_ID(N'[bookrunner].[Scripts]')
)
BEGIN
    CREATE INDEX [IX_Scripts_Name] ON [bookrunner].[Scripts] ([Name]);
    PRINT N'Indeks olusturuldu: IX_Scripts_Name';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Scripts_RunbookId' AND object_id = OBJECT_ID(N'[bookrunner].[Scripts]')
)
BEGIN
    CREATE INDEX [IX_Scripts_RunbookId] ON [bookrunner].[Scripts] ([RunbookId]);
    PRINT N'Indeks olusturuldu: IX_Scripts_RunbookId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskActivities_ActorUserId' AND object_id = OBJECT_ID(N'[bookrunner].[TaskActivities]')
)
BEGIN
    CREATE INDEX [IX_TaskActivities_ActorUserId] ON [bookrunner].[TaskActivities] ([ActorUserId]);
    PRINT N'Indeks olusturuldu: IX_TaskActivities_ActorUserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskActivities_TaskId_CreatedAt' AND object_id = OBJECT_ID(N'[bookrunner].[TaskActivities]')
)
BEGIN
    CREATE INDEX [IX_TaskActivities_TaskId_CreatedAt] ON [bookrunner].[TaskActivities] ([TaskId], [CreatedAt]);
    PRINT N'Indeks olusturuldu: IX_TaskActivities_TaskId_CreatedAt';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskAssignments_GroupId' AND object_id = OBJECT_ID(N'[bookrunner].[TaskAssignments]')
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_GroupId] ON [bookrunner].[TaskAssignments] ([GroupId]);
    PRINT N'Indeks olusturuldu: IX_TaskAssignments_GroupId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskAssignments_TaskId_IsActive' AND object_id = OBJECT_ID(N'[bookrunner].[TaskAssignments]')
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_TaskId_IsActive] ON [bookrunner].[TaskAssignments] ([TaskId], [IsActive]);
    PRINT N'Indeks olusturuldu: IX_TaskAssignments_TaskId_IsActive';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskAssignments_UserId' AND object_id = OBJECT_ID(N'[bookrunner].[TaskAssignments]')
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_UserId] ON [bookrunner].[TaskAssignments] ([UserId]);
    PRINT N'Indeks olusturuldu: IX_TaskAssignments_UserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskComments_AuthorUserId' AND object_id = OBJECT_ID(N'[bookrunner].[TaskComments]')
)
BEGIN
    CREATE INDEX [IX_TaskComments_AuthorUserId] ON [bookrunner].[TaskComments] ([AuthorUserId]);
    PRINT N'Indeks olusturuldu: IX_TaskComments_AuthorUserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskComments_ParentCommentId' AND object_id = OBJECT_ID(N'[bookrunner].[TaskComments]')
)
BEGIN
    CREATE INDEX [IX_TaskComments_ParentCommentId] ON [bookrunner].[TaskComments] ([ParentCommentId]);
    PRINT N'Indeks olusturuldu: IX_TaskComments_ParentCommentId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TaskComments_TaskId_CreatedAt' AND object_id = OBJECT_ID(N'[bookrunner].[TaskComments]')
)
BEGIN
    CREATE INDEX [IX_TaskComments_TaskId_CreatedAt] ON [bookrunner].[TaskComments] ([TaskId], [CreatedAt]);
    PRINT N'Indeks olusturuldu: IX_TaskComments_TaskId_CreatedAt';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Tasks_DependsOnTaskId' AND object_id = OBJECT_ID(N'[bookrunner].[Tasks]')
)
BEGIN
    CREATE INDEX [IX_Tasks_DependsOnTaskId] ON [bookrunner].[Tasks] ([DependsOnTaskId]);
    PRINT N'Indeks olusturuldu: IX_Tasks_DependsOnTaskId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Tasks_RunbookId_Order' AND object_id = OBJECT_ID(N'[bookrunner].[Tasks]')
)
BEGIN
    CREATE INDEX [IX_Tasks_RunbookId_Order] ON [bookrunner].[Tasks] ([RunbookId], [Order]);
    PRINT N'Indeks olusturuldu: IX_Tasks_RunbookId_Order';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Tasks_ScriptId' AND object_id = OBJECT_ID(N'[bookrunner].[Tasks]')
)
BEGIN
    CREATE INDEX [IX_Tasks_ScriptId] ON [bookrunner].[Tasks] ([ScriptId]);
    PRINT N'Indeks olusturuldu: IX_Tasks_ScriptId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Tasks_Status' AND object_id = OBJECT_ID(N'[bookrunner].[Tasks]')
)
BEGIN
    CREATE INDEX [IX_Tasks_Status] ON [bookrunner].[Tasks] ([Status]);
    PRINT N'Indeks olusturuldu: IX_Tasks_Status';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_UserGroups_GroupId' AND object_id = OBJECT_ID(N'[bookrunner].[UserGroups]')
)
BEGIN
    CREATE INDEX [IX_UserGroups_GroupId] ON [bookrunner].[UserGroups] ([GroupId]);
    PRINT N'Indeks olusturuldu: IX_UserGroups_GroupId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Badges_Code' AND object_id = OBJECT_ID(N'[bookrunner].[Badges]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Badges_Code] ON [bookrunner].[Badges] ([Code]);
    PRINT N'Indeks olusturuldu: IX_Badges_Code';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_GamificationEvents_CreatedAt' AND object_id = OBJECT_ID(N'[bookrunner].[GamificationEvents]')
)
BEGIN
    CREATE INDEX [IX_GamificationEvents_CreatedAt] ON [bookrunner].[GamificationEvents] ([CreatedAt]);
    PRINT N'Indeks olusturuldu: IX_GamificationEvents_CreatedAt';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_GamificationEvents_RunbookId' AND object_id = OBJECT_ID(N'[bookrunner].[GamificationEvents]')
)
BEGIN
    CREATE INDEX [IX_GamificationEvents_RunbookId] ON [bookrunner].[GamificationEvents] ([RunbookId]);
    PRINT N'Indeks olusturuldu: IX_GamificationEvents_RunbookId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_GamificationEvents_RunbookTaskId' AND object_id = OBJECT_ID(N'[bookrunner].[GamificationEvents]')
)
BEGIN
    CREATE INDEX [IX_GamificationEvents_RunbookTaskId] ON [bookrunner].[GamificationEvents] ([RunbookTaskId]);
    PRINT N'Indeks olusturuldu: IX_GamificationEvents_RunbookTaskId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_GamificationEvents_UserId_EventType' AND object_id = OBJECT_ID(N'[bookrunner].[GamificationEvents]')
)
BEGIN
    CREATE INDEX [IX_GamificationEvents_UserId_EventType] ON [bookrunner].[GamificationEvents] ([UserId], [EventType]);
    PRINT N'Indeks olusturuldu: IX_GamificationEvents_UserId_EventType';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_UserBadges_BadgeId' AND object_id = OBJECT_ID(N'[bookrunner].[UserBadges]')
)
BEGIN
    CREATE INDEX [IX_UserBadges_BadgeId] ON [bookrunner].[UserBadges] ([BadgeId]);
    PRINT N'Indeks olusturuldu: IX_UserBadges_BadgeId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_UserBadges_UserId_BadgeId' AND object_id = OBJECT_ID(N'[bookrunner].[UserBadges]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserBadges_UserId_BadgeId] ON [bookrunner].[UserBadges] ([UserId], [BadgeId]);
    PRINT N'Indeks olusturuldu: IX_UserBadges_UserId_BadgeId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RunbookCollaborators_RunbookId_UserId' AND object_id = OBJECT_ID(N'[bookrunner].[RunbookCollaborators]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_RunbookCollaborators_RunbookId_UserId] ON [bookrunner].[RunbookCollaborators] ([RunbookId], [UserId]);
    PRINT N'Indeks olusturuldu: IX_RunbookCollaborators_RunbookId_UserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RunbookCollaborators_UserId' AND object_id = OBJECT_ID(N'[bookrunner].[RunbookCollaborators]')
)
BEGIN
    CREATE INDEX [IX_RunbookCollaborators_UserId] ON [bookrunner].[RunbookCollaborators] ([UserId]);
    PRINT N'Indeks olusturuldu: IX_RunbookCollaborators_UserId';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_DisplayName' AND object_id = OBJECT_ID(N'[bookrunner].[Users]')
)
BEGIN
    CREATE INDEX [IX_Users_DisplayName] ON [bookrunner].[Users] ([DisplayName]);
    PRINT N'Indeks olusturuldu: IX_Users_DisplayName';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_SamAccountName' AND object_id = OBJECT_ID(N'[bookrunner].[Users]')
)
BEGIN
    CREATE INDEX [IX_Users_SamAccountName] ON [bookrunner].[Users] ([SamAccountName]);
    PRINT N'Indeks olusturuldu: IX_Users_SamAccountName';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_Sid' AND object_id = OBJECT_ID(N'[bookrunner].[Users]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Sid] ON [bookrunner].[Users] ([Sid]);
    PRINT N'Indeks olusturuldu: IX_Users_Sid';
END
GO

/* ---------------------------------------------------------------------------
   Iliskiler (foreign key)

   Her biri kendi basina kontrol edilir ve TRY/CATCH icindedir: biri
   basarisiz olursa uyari basilir, script durmadan digerlerine gecer.
   --------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Runbooks_Runbooks_SourceTemplateId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[Runbooks] WITH CHECK
            ADD CONSTRAINT [FK_Runbooks_Runbooks_SourceTemplateId] FOREIGN KEY ([SourceTemplateId])
            REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_Runbooks_Runbooks_SourceTemplateId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_Runbooks_Runbooks_SourceTemplateId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Runbooks_Users_OwnerUserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[Runbooks] WITH CHECK
            ADD CONSTRAINT [FK_Runbooks_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_Runbooks_Users_OwnerUserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_Runbooks_Users_OwnerUserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserGroups_Groups_GroupId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[UserGroups] WITH CHECK
            ADD CONSTRAINT [FK_UserGroups_Groups_GroupId] FOREIGN KEY ([GroupId])
            REFERENCES [bookrunner].[Groups] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_UserGroups_Groups_GroupId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_UserGroups_Groups_GroupId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserGroups_Users_UserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[UserGroups] WITH CHECK
            ADD CONSTRAINT [FK_UserGroups_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_UserGroups_Users_UserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_UserGroups_Users_UserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Scripts_Runbooks_RunbookId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[Scripts] WITH CHECK
            ADD CONSTRAINT [FK_Scripts_Runbooks_RunbookId] FOREIGN KEY ([RunbookId])
            REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_Scripts_Runbooks_RunbookId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_Scripts_Runbooks_RunbookId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Runbooks_RunbookId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[Tasks] WITH CHECK
            ADD CONSTRAINT [FK_Tasks_Runbooks_RunbookId] FOREIGN KEY ([RunbookId])
            REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_Tasks_Runbooks_RunbookId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_Tasks_Runbooks_RunbookId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Scripts_ScriptId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[Tasks] WITH CHECK
            ADD CONSTRAINT [FK_Tasks_Scripts_ScriptId] FOREIGN KEY ([ScriptId])
            REFERENCES [bookrunner].[Scripts] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_Tasks_Scripts_ScriptId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_Tasks_Scripts_ScriptId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Tasks_DependsOnTaskId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[Tasks] WITH CHECK
            ADD CONSTRAINT [FK_Tasks_Tasks_DependsOnTaskId] FOREIGN KEY ([DependsOnTaskId])
            REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_Tasks_Tasks_DependsOnTaskId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_Tasks_Tasks_DependsOnTaskId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ScriptExecutions_Scripts_ScriptId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[ScriptExecutions] WITH CHECK
            ADD CONSTRAINT [FK_ScriptExecutions_Scripts_ScriptId] FOREIGN KEY ([ScriptId])
            REFERENCES [bookrunner].[Scripts] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_ScriptExecutions_Scripts_ScriptId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_ScriptExecutions_Scripts_ScriptId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ScriptExecutions_Tasks_TaskId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[ScriptExecutions] WITH CHECK
            ADD CONSTRAINT [FK_ScriptExecutions_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_ScriptExecutions_Tasks_TaskId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_ScriptExecutions_Tasks_TaskId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ScriptExecutions_Users_ExecutedByUserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[ScriptExecutions] WITH CHECK
            ADD CONSTRAINT [FK_ScriptExecutions_Users_ExecutedByUserId] FOREIGN KEY ([ExecutedByUserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_ScriptExecutions_Users_ExecutedByUserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_ScriptExecutions_Users_ExecutedByUserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskActivities_Tasks_TaskId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskActivities] WITH CHECK
            ADD CONSTRAINT [FK_TaskActivities_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_TaskActivities_Tasks_TaskId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskActivities_Tasks_TaskId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskActivities_Users_ActorUserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskActivities] WITH CHECK
            ADD CONSTRAINT [FK_TaskActivities_Users_ActorUserId] FOREIGN KEY ([ActorUserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_TaskActivities_Users_ActorUserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskActivities_Users_ActorUserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskAssignments_Groups_GroupId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskAssignments] WITH CHECK
            ADD CONSTRAINT [FK_TaskAssignments_Groups_GroupId] FOREIGN KEY ([GroupId])
            REFERENCES [bookrunner].[Groups] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_TaskAssignments_Groups_GroupId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskAssignments_Groups_GroupId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskAssignments_Tasks_TaskId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskAssignments] WITH CHECK
            ADD CONSTRAINT [FK_TaskAssignments_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_TaskAssignments_Tasks_TaskId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskAssignments_Tasks_TaskId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskAssignments_Users_UserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskAssignments] WITH CHECK
            ADD CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_TaskAssignments_Users_UserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskAssignments_Users_UserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskComments_TaskComments_ParentCommentId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskComments] WITH CHECK
            ADD CONSTRAINT [FK_TaskComments_TaskComments_ParentCommentId] FOREIGN KEY ([ParentCommentId])
            REFERENCES [bookrunner].[TaskComments] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_TaskComments_TaskComments_ParentCommentId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskComments_TaskComments_ParentCommentId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskComments_Tasks_TaskId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskComments] WITH CHECK
            ADD CONSTRAINT [FK_TaskComments_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_TaskComments_Tasks_TaskId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskComments_Tasks_TaskId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskComments_Users_AuthorUserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[TaskComments] WITH CHECK
            ADD CONSTRAINT [FK_TaskComments_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_TaskComments_Users_AuthorUserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_TaskComments_Users_AuthorUserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GamificationEvents_Runbooks_RunbookId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[GamificationEvents] WITH CHECK
            ADD CONSTRAINT [FK_GamificationEvents_Runbooks_RunbookId] FOREIGN KEY ([RunbookId])
            REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_GamificationEvents_Runbooks_RunbookId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_GamificationEvents_Runbooks_RunbookId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GamificationEvents_Tasks_RunbookTaskId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[GamificationEvents] WITH CHECK
            ADD CONSTRAINT [FK_GamificationEvents_Tasks_RunbookTaskId] FOREIGN KEY ([RunbookTaskId])
            REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE NO ACTION;
        PRINT N'Iliski eklendi: FK_GamificationEvents_Tasks_RunbookTaskId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_GamificationEvents_Tasks_RunbookTaskId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GamificationEvents_Users_UserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[GamificationEvents] WITH CHECK
            ADD CONSTRAINT [FK_GamificationEvents_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_GamificationEvents_Users_UserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_GamificationEvents_Users_UserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserBadges_Badges_BadgeId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[UserBadges] WITH CHECK
            ADD CONSTRAINT [FK_UserBadges_Badges_BadgeId] FOREIGN KEY ([BadgeId])
            REFERENCES [bookrunner].[Badges] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_UserBadges_Badges_BadgeId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_UserBadges_Badges_BadgeId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserBadges_Users_UserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[UserBadges] WITH CHECK
            ADD CONSTRAINT [FK_UserBadges_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_UserBadges_Users_UserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_UserBadges_Users_UserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RunbookCollaborators_Runbooks_RunbookId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[RunbookCollaborators] WITH CHECK
            ADD CONSTRAINT [FK_RunbookCollaborators_Runbooks_RunbookId] FOREIGN KEY ([RunbookId])
            REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_RunbookCollaborators_Runbooks_RunbookId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_RunbookCollaborators_Runbooks_RunbookId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RunbookCollaborators_Users_UserId')
BEGIN
    BEGIN TRY
        ALTER TABLE [bookrunner].[RunbookCollaborators] WITH CHECK
            ADD CONSTRAINT [FK_RunbookCollaborators_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [bookrunner].[Users] ([Id]) ON DELETE CASCADE;
        PRINT N'Iliski eklendi: FK_RunbookCollaborators_Users_UserId';
    END TRY
    BEGIN CATCH
        PRINT N'UYARI: FK_RunbookCollaborators_Users_UserId eklenemedi -> ' + ERROR_MESSAGE();
    END CATCH
END
GO

/* ---------------------------------------------------------------------------
   Rozet katalogu (seed)

   GamificationService bu kodlara gore esik kontrolu yapar; Ad/Aciklama
   sonradan degistirilse bile kod sabit kalmalidir.
   --------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM [bookrunner].[Badges] WHERE [Code] = N'FIRST_TASK')
    INSERT INTO [bookrunner].[Badges] ([Id], [Code], [Name], [Description], [Icon], [SortOrder])
    VALUES (NEWID(), N'FIRST_TASK', N'Ilk Adim', N'Ilk gorevini tamamladin.', N'bi-flag', 1);

IF NOT EXISTS (SELECT 1 FROM [bookrunner].[Badges] WHERE [Code] = N'TASKS_10')
    INSERT INTO [bookrunner].[Badges] ([Id], [Code], [Name], [Description], [Icon], [SortOrder])
    VALUES (NEWID(), N'TASKS_10', N'10 Gorev', N'10 gorev tamamladin.', N'bi-check2-circle', 2);

IF NOT EXISTS (SELECT 1 FROM [bookrunner].[Badges] WHERE [Code] = N'TASKS_50')
    INSERT INTO [bookrunner].[Badges] ([Id], [Code], [Name], [Description], [Icon], [SortOrder])
    VALUES (NEWID(), N'TASKS_50', N'50 Gorev', N'50 gorev tamamladin.', N'bi-check2-all', 3);

IF NOT EXISTS (SELECT 1 FROM [bookrunner].[Badges] WHERE [Code] = N'TASKS_100')
    INSERT INTO [bookrunner].[Badges] ([Id], [Code], [Name], [Description], [Icon], [SortOrder])
    VALUES (NEWID(), N'TASKS_100', N'100 Gorev', N'100 gorev tamamladin.', N'bi-trophy', 4);

IF NOT EXISTS (SELECT 1 FROM [bookrunner].[Badges] WHERE [Code] = N'FIRST_RUNBOOK')
    INSERT INTO [bookrunner].[Badges] ([Id], [Code], [Name], [Description], [Icon], [SortOrder])
    VALUES (NEWID(), N'FIRST_RUNBOOK', N'Ilk Runbook', N'Sahibi oldugun ilk runbook''u tamamladin.', N'bi-journal-check', 5);

IF NOT EXISTS (SELECT 1 FROM [bookrunner].[Badges] WHERE [Code] = N'COMMENTS_20')
    INSERT INTO [bookrunner].[Badges] ([Id], [Code], [Name], [Description], [Icon], [SortOrder])
    VALUES (NEWID(), N'COMMENTS_20', N'Belgeci', N'20 goreve yorum/not biraktin.', N'bi-chat-square-text', 6);
GO

/* ---------------------------------------------------------------------------
   Tamamlanma kontrolu

   Migration gecmisine yalniz TUM tablolar gercekten varsa "tamamlandi"
   yazilir. Boylece yarim kalan bir kurulumda uygulama yanlislikla "sema
   hazir" sanmaz; bu script'i tekrar calistirdiginizda kaldigi yerden devam
   eder.
   --------------------------------------------------------------------------- */

DECLARE @expectedTables int = 18;
DECLARE @actualTables int = (
    SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'bookrunner')
);

IF @actualTables >= @expectedTables
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM [bookrunner].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260827115149_InitialCreate'
    )
    BEGIN
        INSERT INTO [bookrunner].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260827115149_InitialCreate', N'9.0.19');
    END

    IF NOT EXISTS (
        SELECT 1 FROM [bookrunner].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260831071620_AddGamification'
    )
    BEGIN
        INSERT INTO [bookrunner].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260831071620_AddGamification', N'9.0.19');
    END

    IF NOT EXISTS (
        SELECT 1 FROM [bookrunner].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260901060033_AddRunbookCollaborators'
    )
    BEGIN
        INSERT INTO [bookrunner].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260901060033_AddRunbookCollaborators', N'9.0.19');
    END

    IF NOT EXISTS (
        SELECT 1 FROM [bookrunner].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260901062644_AddAppGroupIsTeam'
    )
    BEGIN
        INSERT INTO [bookrunner].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260901062644_AddAppGroupIsTeam', N'9.0.19');
    END

    IF NOT EXISTS (
        SELECT 1 FROM [bookrunner].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260902064932_AddRunbookProgramName'
    )
    BEGIN
        INSERT INTO [bookrunner].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260902064932_AddRunbookProgramName', N'9.0.19');
    END

    PRINT N'';
    PRINT N'TAMAMLANDI: ' + CAST(@actualTables AS nvarchar(10)) + N' / ' + CAST(@expectedTables AS nvarchar(10)) + N' tablo hazir.';
END
ELSE
BEGIN
    PRINT N'';
    PRINT N'-------------------------------------------------------------------';
    PRINT N'EKSIK: ' + CAST(@actualTables AS nvarchar(10)) + N' / ' + CAST(@expectedTables AS nvarchar(10)) + N' tablo olusturulabildi.';
    PRINT N'Yukaridaki mesajlarda hangi tablonun basarisiz oldugunu gorebilirsiniz.';
    PRINT N'Sorunu giderdikten sonra bu script''i TEKRAR calistirin; yalnizca';
    PRINT N'eksik kalanlar tamamlanir, mevcut olanlara dokunulmaz.';
    PRINT N'-------------------------------------------------------------------';
END
GO
