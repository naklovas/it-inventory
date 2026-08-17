IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FisGunlukOzet' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.FisGunlukOzet
    (
        Tarih               DATE            NOT NULL,
        Kanal               NVARCHAR(20)    NOT NULL,
        ToplamFisSayisi     BIGINT          NOT NULL,
        GuncellemeZamani    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FisGunlukOzet PRIMARY KEY (Tarih, Kanal)
    );
END
