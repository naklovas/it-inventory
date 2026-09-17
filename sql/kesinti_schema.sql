-- OutageHelper (Kesinti Analiz Uygulamasi) - SQL Server semasi
-- Idempotent: her tablo yalnizca yoksa olusturulur.

-- Kategori: kapali liste. Siniflandirma asamasinda LLM bu listeden secim yapar.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Kategori' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Kategori
    (
        KategoriId      INT IDENTITY(1,1)  NOT NULL,
        Ad              NVARCHAR(200)      NOT NULL,
        Aciklama        NVARCHAR(500)      NULL,
        AktifMi         BIT                NOT NULL DEFAULT 1,
        OlusturmaZamani DATETIME2          NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Kategori PRIMARY KEY (KategoriId),
        CONSTRAINT UQ_Kategori_Ad UNIQUE (Ad)
    );
END
GO

-- KesintiDokuman: 1. asamada okunan her .docx dosyasi icin bir kayit.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KesintiDokuman' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.KesintiDokuman
    (
        KesintiDokumanId    INT IDENTITY(1,1)      NOT NULL,
        DosyaYolu           NVARCHAR(1000)         NOT NULL,
        DosyaHash           CHAR(64)               NOT NULL,   -- SHA-256, tekrar isleme onlemek icin
        HamMetin             NVARCHAR(MAX)          NOT NULL,   -- her zaman saklanan orijinal metin
        FormatDurumu        NVARCHAR(30)           NOT NULL,   -- 'Standart' | 'Standart disi'
        IslenmeZamani       DATETIME2              NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_KesintiDokuman PRIMARY KEY (KesintiDokumanId),
        CONSTRAINT UQ_KesintiDokuman_Hash UNIQUE (DosyaHash)
    );
END
GO

-- KesintiKayit: dokumandan (kural tabanli veya AI ile) cikarilan yapilandirilmis alanlar.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KesintiKayit' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.KesintiKayit
    (
        KesintiKayitId      INT IDENTITY(1,1)      NOT NULL,
        KesintiDokumanId    INT                    NOT NULL,
        Tarih               DATETIME2              NULL,
        SureDakika          INT                    NULL,
        EtkilenenSistemler  NVARCHAR(1000)         NULL,
        Neden               NVARCHAR(MAX)          NULL,
        AlinanOnlemler      NVARCHAR(MAX)          NULL,
        KategoriId          INT                    NULL,
        SiniflandirmaDurumu NVARCHAR(30)           NOT NULL DEFAULT 'Bekliyor', -- 'Bekliyor' | 'Siniflandirildi'
        OlusturmaZamani     DATETIME2              NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_KesintiKayit PRIMARY KEY (KesintiKayitId),
        CONSTRAINT FK_KesintiKayit_Dokuman FOREIGN KEY (KesintiDokumanId) REFERENCES dbo.KesintiDokuman (KesintiDokumanId),
        CONSTRAINT FK_KesintiKayit_Kategori FOREIGN KEY (KategoriId) REFERENCES dbo.Kategori (KategoriId)
    );
END
GO

-- KesintiSiniflandirma: 2. asamada LLM'in urettigi ham cikti + model bilgisi + elle duzeltme.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KesintiSiniflandirma' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.KesintiSiniflandirma
    (
        KesintiSiniflandirmaId INT IDENTITY(1,1)   NOT NULL,
        KesintiKayitId          INT                 NOT NULL,
        ModelAdi                NVARCHAR(200)       NOT NULL,
        ModelCikisiJson         NVARCHAR(MAX)       NOT NULL,   -- LLM'den donen ham JSON
        OnerilenKategoriId      INT                 NULL,
        ElleDuzeltilmisKategoriId INT               NULL,       -- kullanici duzeltmesi (varsa)
        ElleDuzeltenKullanici   NVARCHAR(200)       NULL,
        Tarih                   DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_KesintiSiniflandirma PRIMARY KEY (KesintiSiniflandirmaId),
        CONSTRAINT FK_KesintiSiniflandirma_Kayit FOREIGN KEY (KesintiKayitId) REFERENCES dbo.KesintiKayit (KesintiKayitId),
        CONSTRAINT FK_KesintiSiniflandirma_OnerilenKategori FOREIGN KEY (OnerilenKategoriId) REFERENCES dbo.Kategori (KategoriId),
        CONSTRAINT FK_KesintiSiniflandirma_DuzeltilmisKategori FOREIGN KEY (ElleDuzeltilmisKategoriId) REFERENCES dbo.Kategori (KategoriId)
    );
END
GO

-- DegisiklikKaydi: 3. asamada girdi olan haftalik degisiklik listesi (CSV/Excel'den yuklenir).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DegisiklikKaydi' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DegisiklikKaydi
    (
        DegisiklikKaydiId  INT IDENTITY(1,1)      NOT NULL,
        Sistem              NVARCHAR(200)          NOT NULL,
        DegisiklikTipi      NVARCHAR(200)          NOT NULL,
        Aciklama            NVARCHAR(MAX)          NULL,
        PlanlananTarih      DATETIME2              NULL,
        KaynakDosya         NVARCHAR(1000)         NULL,
        YuklemeZamani       DATETIME2              NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_DegisiklikKaydi PRIMARY KEY (DegisiklikKaydiId)
    );
END
GO

-- RiskDegerlendirme: 3. asama LLM ciktisi (risk seviyesi, dayandigi kesinti ID'leri, onlemler).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RiskDegerlendirme' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.RiskDegerlendirme
    (
        RiskDegerlendirmeId    INT IDENTITY(1,1)   NOT NULL,
        DegisiklikKaydiId      INT                 NOT NULL,
        RiskSeviyesi            NVARCHAR(30)        NOT NULL,   -- 'Dusuk' | 'Orta' | 'Yuksek' | 'Belirsiz' (kayit yoksa)
        DayanilanKesintiIdleriJson NVARCHAR(MAX)    NULL,       -- JSON array of KesintiKayitId
        OnerilenOnlemler        NVARCHAR(MAX)       NULL,
        ModelAdi                NVARCHAR(200)       NOT NULL,
        ModelCikisiJson         NVARCHAR(MAX)       NOT NULL,
        Tarih                   DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RiskDegerlendirme PRIMARY KEY (RiskDegerlendirmeId),
        CONSTRAINT FK_RiskDegerlendirme_Degisiklik FOREIGN KEY (DegisiklikKaydiId) REFERENCES dbo.DegisiklikKaydi (DegisiklikKaydiId)
    );
END
GO

-- Baslangic kategori listesi (kapali liste ornegi - projeye gore genisletilebilir).
IF NOT EXISTS (SELECT 1 FROM dbo.Kategori)
BEGIN
    INSERT INTO dbo.Kategori (Ad, Aciklama) VALUES
        (N'Donanim Arizasi', N'Sunucu, ag cihazi, depolama gibi fiziksel donanim kaynakli kesintiler'),
        (N'Yazilim Hatasi', N'Uygulama veya isletim sistemi hatasi kaynakli kesintiler'),
        (N'Ag/Baglanti Sorunu', N'Ag, internet veya baglanti kaynakli kesintiler'),
        (N'Planli Bakim', N'Onceden planlanmis bakim calismasi kaynakli kesintiler'),
        (N'Elektrik Kesintisi', N'Elektrik altyapisi kaynakli kesintiler'),
        (N'Insan Hatasi', N'Operasyonel/insan hatasi kaynakli kesintiler'),
        (N'Diger', N'Yukaridaki kategorilere uymayan kesintiler');
END
GO
