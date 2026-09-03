-- MailRelay.Service icin sema.
-- MailQueue hem kuyruk hem de gonderim gecmisi (log) olarak kullanilir; Status alani
-- Queued -> Processing -> Sent / Retrying -> Failed akisini temsil eder.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClientApplications' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.ClientApplications
    (
        Id              INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
        Name            NVARCHAR(100)      NOT NULL,
        ApiKey          NVARCHAR(200)      NOT NULL,
        Enabled         BIT                NOT NULL DEFAULT 1,
        CreatedAtUtc    DATETIME2          NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ClientApplications_ApiKey UNIQUE (ApiKey)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RelaySettings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.RelaySettings
    (
        -- Tek satirlik ayar tablosu; Id her zaman 1.
        Id                  INT             NOT NULL PRIMARY KEY CHECK (Id = 1),
        Enabled             BIT             NOT NULL DEFAULT 1,
        Host                NVARCHAR(200)   NOT NULL,
        Port                INT             NOT NULL DEFAULT 25,
        EnableSsl           BIT             NOT NULL DEFAULT 0,
        Username            NVARCHAR(200)   NULL,
        Password            NVARCHAR(500)   NULL,
        FromAddress         NVARCHAR(200)   NOT NULL,
        FromDisplayName     NVARCHAR(200)   NULL,
        MaxConcurrentSend   INT             NOT NULL DEFAULT 4,
        UpdatedAtUtc        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy           NVARCHAR(200)   NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MailQueue' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.MailQueue
    (
        Id                  BIGINT IDENTITY(1,1)   NOT NULL PRIMARY KEY,
        ClientApplicationId INT                     NULL REFERENCES dbo.ClientApplications(Id),
        RequestedByUsername NVARCHAR(100)           NULL,
        RequestedByTeam     NVARCHAR(200)           NULL,
        ToAddresses         NVARCHAR(MAX)           NOT NULL,
        CcAddresses         NVARCHAR(MAX)           NULL,
        BccAddresses        NVARCHAR(MAX)           NULL,
        Subject             NVARCHAR(500)           NOT NULL,
        Body                NVARCHAR(MAX)           NOT NULL,
        IsBodyHtml          BIT                     NOT NULL DEFAULT 1,
        Priority            TINYINT                 NOT NULL DEFAULT 3, -- 1=yuksek .. 5=dusuk
        Status              NVARCHAR(20)            NOT NULL DEFAULT 'Queued',
        Attempts            INT                     NOT NULL DEFAULT 0,
        MaxAttempts         INT                     NOT NULL DEFAULT 5,
        NextAttemptAtUtc    DATETIME2               NULL,
        LastError           NVARCHAR(2000)          NULL,
        CorrelationId       NVARCHAR(100)           NULL,
        SourcePort          INT                     NULL,
        CreatedAtUtc        DATETIME2               NOT NULL DEFAULT SYSUTCDATETIME(),
        SentAtUtc           DATETIME2               NULL
    );

    CREATE INDEX IX_MailQueue_Status_NextAttempt ON dbo.MailQueue (Status, NextAttemptAtUtc) INCLUDE (Priority, CreatedAtUtc);
    CREATE INDEX IX_MailQueue_RequestedByUsername ON dbo.MailQueue (RequestedByUsername);
    CREATE INDEX IX_MailQueue_CreatedAtUtc ON dbo.MailQueue (CreatedAtUtc DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MailAttachments' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.MailAttachments
    (
        Id              BIGINT IDENTITY(1,1)   NOT NULL PRIMARY KEY,
        MailQueueId     BIGINT                  NOT NULL REFERENCES dbo.MailQueue(Id) ON DELETE CASCADE,
        FileName        NVARCHAR(260)           NOT NULL,
        ContentType     NVARCHAR(200)           NULL,
        Content         VARBINARY(MAX)          NOT NULL
    );

    CREATE INDEX IX_MailAttachments_MailQueueId ON dbo.MailAttachments (MailQueueId);
END
GO

-- Baslangic relay ayari (appsettings.json > SmtpSettings ile ayni degerlerle doldurulup
-- gercek deger admin panelinden guncellenebilir). Yer tutucu degerlerle eklenir, INSERT
-- sonrasi admin panelinden Host/Username/Password/FromAddress guncellenmelidir.
IF NOT EXISTS (SELECT 1 FROM dbo.RelaySettings WHERE Id = 1)
BEGIN
    INSERT INTO dbo.RelaySettings (Id, Enabled, Host, Port, EnableSsl, Username, Password, FromAddress, FromDisplayName, MaxConcurrentSend)
    VALUES (1, 0, 'smtp.local', 25, 0, NULL, NULL, 'noreply@example.com', 'IT Inventory Mail Servisi', 4);
END
GO
