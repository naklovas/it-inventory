IF OBJECT_ID(N'[bookrunner].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'bookrunner') IS NULL EXEC(N'CREATE SCHEMA [bookrunner];');
    CREATE TABLE [bookrunner].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'bookrunner') IS NULL EXEC(N'CREATE SCHEMA [bookrunner];');
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        [LastSyncedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Groups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE TABLE [bookrunner].[RoleMappings] (
        [Id] uniqueidentifier NOT NULL,
        [GroupSid] nvarchar(184) NOT NULL,
        [GroupName] nvarchar(256) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_RoleMappings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE TABLE [bookrunner].[Runbooks] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(32) NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [IsTemplate] bit NOT NULL,
        [TemplateCategory] nvarchar(100) NULL,
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
        CONSTRAINT [PK_Runbooks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Runbooks_Runbooks_SourceTemplateId] FOREIGN KEY ([SourceTemplateId]) REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Runbooks_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE TABLE [bookrunner].[UserGroups] (
        [UserId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [SyncedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserGroups] PRIMARY KEY ([UserId], [GroupId]),
        CONSTRAINT [FK_UserGroups_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [bookrunner].[Groups] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserGroups_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [bookrunner].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        CONSTRAINT [PK_Scripts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Scripts_Runbooks_RunbookId] FOREIGN KEY ([RunbookId]) REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tasks_Runbooks_RunbookId] FOREIGN KEY ([RunbookId]) REFERENCES [bookrunner].[Runbooks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Tasks_Scripts_ScriptId] FOREIGN KEY ([ScriptId]) REFERENCES [bookrunner].[Scripts] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tasks_Tasks_DependsOnTaskId] FOREIGN KEY ([DependsOnTaskId]) REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        CONSTRAINT [PK_ScriptExecutions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ScriptExecutions_Scripts_ScriptId] FOREIGN KEY ([ScriptId]) REFERENCES [bookrunner].[Scripts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ScriptExecutions_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScriptExecutions_Users_ExecutedByUserId] FOREIGN KEY ([ExecutedByUserId]) REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        CONSTRAINT [PK_TaskActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskActivities_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskActivities_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        CONSTRAINT [CK_TaskAssignments_Target] CHECK (([AssigneeType] = 0 AND [UserId] IS NOT NULL AND [GroupId] IS NULL) OR ([AssigneeType] = 1 AND [GroupId] IS NOT NULL AND [UserId] IS NULL)),
        CONSTRAINT [FK_TaskAssignments_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [bookrunner].[Groups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskAssignments_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
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
        CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskComments_TaskComments_ParentCommentId] FOREIGN KEY ([ParentCommentId]) REFERENCES [bookrunner].[TaskComments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskComments_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [bookrunner].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskComments_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [bookrunner].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [bookrunner].[AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_RunbookId] ON [bookrunner].[AuditLogs] ([RunbookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [bookrunner].[AuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserName] ON [bookrunner].[AuditLogs] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmailOutbox_Status_NextAttemptAt] ON [bookrunner].[EmailOutbox] ([Status], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_Name] ON [bookrunner].[Groups] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Groups_Sid] ON [bookrunner].[Groups] ([Sid]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoleMappings_GroupSid_Role] ON [bookrunner].[RoleMappings] ([GroupSid], [Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Runbooks_Code] ON [bookrunner].[Runbooks] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Runbooks_IsTemplate_Status] ON [bookrunner].[Runbooks] ([IsTemplate], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Runbooks_OwnerUserId] ON [bookrunner].[Runbooks] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Runbooks_PlannedStart] ON [bookrunner].[Runbooks] ([PlannedStart]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Runbooks_ServiceManagerWorkItemId] ON [bookrunner].[Runbooks] ([ServiceManagerWorkItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Runbooks_SourceTemplateId] ON [bookrunner].[Runbooks] ([SourceTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_ExecutedByUserId] ON [bookrunner].[ScriptExecutions] ([ExecutedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_ScriptId] ON [bookrunner].[ScriptExecutions] ([ScriptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_StartedAt] ON [bookrunner].[ScriptExecutions] ([StartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_TaskId] ON [bookrunner].[ScriptExecutions] ([TaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Scripts_Name] ON [bookrunner].[Scripts] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Scripts_RunbookId] ON [bookrunner].[Scripts] ([RunbookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskActivities_ActorUserId] ON [bookrunner].[TaskActivities] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskActivities_TaskId_CreatedAt] ON [bookrunner].[TaskActivities] ([TaskId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_GroupId] ON [bookrunner].[TaskAssignments] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_TaskId_IsActive] ON [bookrunner].[TaskAssignments] ([TaskId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_UserId] ON [bookrunner].[TaskAssignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskComments_AuthorUserId] ON [bookrunner].[TaskComments] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskComments_ParentCommentId] ON [bookrunner].[TaskComments] ([ParentCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskComments_TaskId_CreatedAt] ON [bookrunner].[TaskComments] ([TaskId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tasks_DependsOnTaskId] ON [bookrunner].[Tasks] ([DependsOnTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tasks_RunbookId_Order] ON [bookrunner].[Tasks] ([RunbookId], [Order]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tasks_ScriptId] ON [bookrunner].[Tasks] ([ScriptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tasks_Status] ON [bookrunner].[Tasks] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserGroups_GroupId] ON [bookrunner].[UserGroups] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_DisplayName] ON [bookrunner].[Users] ([DisplayName]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_SamAccountName] ON [bookrunner].[Users] ([SamAccountName]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Sid] ON [bookrunner].[Users] ([Sid]);
END;

IF NOT EXISTS (
    SELECT * FROM [bookrunner].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827115149_InitialCreate'
)
BEGIN
    INSERT INTO [bookrunner].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827115149_InitialCreate', N'9.0.19');
END;

COMMIT;
GO

