IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FisSayilariOzet' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.FisSayilariOzet
    (
        Zaman               DATETIME2       NOT NULL,
        Kanal               NVARCHAR(20)    NOT NULL,
        ToplamFisSayisi     BIGINT          NOT NULL,
        GuncellemeZamani    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FisSayilariOzet PRIMARY KEY (Zaman, Kanal)
    );
END
