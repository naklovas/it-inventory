IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WizardField' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.WizardField
    (
        FieldId                 INT IDENTITY(1,1) PRIMARY KEY,
        FieldKey                NVARCHAR(50)    NOT NULL UNIQUE,
        Label                   NVARCHAR(200)   NOT NULL,
        FieldType               NVARCHAR(20)    NOT NULL, -- 'SingleSelect' | 'MultiSelect'
        AllowOther              BIT             NOT NULL DEFAULT 1,
        SortOrder               INT             NOT NULL,
        ConditionalOnFieldKey   NVARCHAR(50)    NULL, -- baska bir FieldKey; o alan asagidaki degere
                                                        -- esitse bu alan wizard'da gizlenir
        ConditionalHiddenValue  NVARCHAR(200)   NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WizardOption' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.WizardOption
    (
        OptionId    INT IDENTITY(1,1) PRIMARY KEY,
        FieldId     INT             NOT NULL REFERENCES dbo.WizardField(FieldId) ON DELETE CASCADE,
        OptionText  NVARCHAR(200)   NOT NULL,
        SortOrder   INT             NOT NULL
    );
END;
GO

-- Ilk kurulumda alanlari/secenekleri bir kere doldurur. Tablo zaten doluysa (daha sonra
-- elle/DB'den duzenlenmis olabilir) hicbir seyi degistirmez.
IF NOT EXISTS (SELECT 1 FROM dbo.WizardField)
BEGIN
    DECLARE @FieldId_AppType INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'AppType', N'Uygulama tipi', N'SingleSelect', 1, 10, NULL, NULL);
    SET @FieldId_AppType = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_AppType, N'Web API', 1),
        (@FieldId_AppType, N'Web App (MVC/Razor)', 2),
        (@FieldId_AppType, N'Blazor (Server/WASM)', 3),
        (@FieldId_AppType, N'WPF', 4),
        (@FieldId_AppType, N'WinForms', 5),
        (@FieldId_AppType, N'Console/CLI', 6),
        (@FieldId_AppType, N'Windows Service', 7),
        (@FieldId_AppType, N'MAUI', 8);

    DECLARE @FieldId_Domain INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Domain', N'Amaç/domain', N'SingleSelect', 1, 20, NULL, NULL);
    SET @FieldId_Domain = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Domain, N'CRUD/veri yönetimi', 1),
        (@FieldId_Domain, N'Envanter-stok takibi', 2),
        (@FieldId_Domain, N'Muhasebe/finans', 3),
        (@FieldId_Domain, N'Raporlama/dashboard', 4),
        (@FieldId_Domain, N'Otomasyon/entegrasyon scripti', 5),
        (@FieldId_Domain, N'Onay/iş akışı sistemi', 6);

    DECLARE @FieldId_Scale INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Scale', N'Ölçek', N'SingleSelect', 0, 30, NULL, NULL);
    SET @FieldId_Scale = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Scale, N'Kişisel/tek kullanıcı', 1),
        (@FieldId_Scale, N'Küçük ekip (dahili)', 2),
        (@FieldId_Scale, N'Kurumsal çok kullanıcılı', 3),
        (@FieldId_Scale, N'İnternete açık', 4);

    DECLARE @FieldId_DataLayer INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'DataLayer', N'Veri katmanı', N'SingleSelect', 0, 40, NULL, NULL);
    SET @FieldId_DataLayer = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_DataLayer, N'Yok (bellek içi)', 1),
        (@FieldId_DataLayer, N'SQLite', 2),
        (@FieldId_DataLayer, N'SQL Server', 3),
        (@FieldId_DataLayer, N'PostgreSQL', 4),
        (@FieldId_DataLayer, N'MySQL', 5),
        (@FieldId_DataLayer, N'Dosya tabanlı (JSON/Excel/CSV)', 6);

    DECLARE @FieldId_AccessMethod INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'AccessMethod', N'Veri erişim yöntemi', N'SingleSelect', 0, 50, N'DataLayer', N'Yok (bellek içi)');
    SET @FieldId_AccessMethod = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_AccessMethod, N'Entity Framework Core', 1),
        (@FieldId_AccessMethod, N'Dapper', 2),
        (@FieldId_AccessMethod, N'ADO.NET (raw)', 3);

    DECLARE @FieldId_Auth INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Auth', N'Kimlik doğrulama', N'SingleSelect', 0, 60, NULL, NULL);
    SET @FieldId_Auth = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Auth, N'Yok', 1),
        (@FieldId_Auth, N'ASP.NET Core Identity', 2),
        (@FieldId_Auth, N'JWT', 3),
        (@FieldId_Auth, N'Windows/AD Auth', 4),
        (@FieldId_Auth, N'OAuth (Google/Microsoft)', 5),
        (@FieldId_Auth, N'Basit kullanıcı-şifre', 6);

    DECLARE @FieldId_Architecture INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Architecture', N'Mimari', N'SingleSelect', 0, 70, NULL, NULL);
    SET @FieldId_Architecture = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Architecture, N'Basit tek proje', 1),
        (@FieldId_Architecture, N'Katmanlı (N-tier)', 2),
        (@FieldId_Architecture, N'Clean Architecture', 3),
        (@FieldId_Architecture, N'MVVM (masaüstü)', 4),
        (@FieldId_Architecture, N'Vertical Slice', 5);

    DECLARE @FieldId_BackendArchitecture INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'BackendArchitecture', N'Backend mimarisi', N'SingleSelect', 0, 80, NULL, NULL);
    SET @FieldId_BackendArchitecture = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_BackendArchitecture, N'Monolit (arayüzle tek proje)', 1),
        (@FieldId_BackendArchitecture, N'Ayrı REST API + ayrı frontend', 2),
        (@FieldId_BackendArchitecture, N'Ayrı GraphQL API + frontend', 3),
        (@FieldId_BackendArchitecture, N'Sadece API (frontend yok)', 4);

    DECLARE @FieldId_ApiDocs INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ApiDocs', N'API dokümantasyonu', N'SingleSelect', 0, 90, N'BackendArchitecture', N'Monolit (arayüzle tek proje)');
    SET @FieldId_ApiDocs = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_ApiDocs, N'Swagger/OpenAPI ekle', 1),
        (@FieldId_ApiDocs, N'Gerek yok', 2);

    DECLARE @FieldId_Features INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Features', N'Temel özellikler', N'MultiSelect', 1, 100, NULL, NULL);
    SET @FieldId_Features = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Features, N'Listeleme/filtreleme', 1),
        (@FieldId_Features, N'CRUD ekranları', 2),
        (@FieldId_Features, N'Excel import/export', 3),
        (@FieldId_Features, N'PDF export', 4),
        (@FieldId_Features, N'E-posta gönderimi', 5),
        (@FieldId_Features, N'Zamanlanmış görev', 6),
        (@FieldId_Features, N'Dosya yükleme', 7),
        (@FieldId_Features, N'Arama', 8),
        (@FieldId_Features, N'Log/audit trail', 9),
        (@FieldId_Features, N'Bildirim', 10),
        (@FieldId_Features, N'3. parti API entegrasyonu', 11);

    DECLARE @FieldId_UiStyle INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'UiStyle', N'UI stili', N'SingleSelect', 0, 110, NULL, NULL);
    SET @FieldId_UiStyle = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_UiStyle, N'Minimal', 1),
        (@FieldId_UiStyle, N'Modern (Bootstrap/MudBlazor/MaterialDesign)', 2),
        (@FieldId_UiStyle, N'Kurumsal/tablo ağırlıklı', 3),
        (@FieldId_UiStyle, N'Dashboard/grafikli', 4);

    DECLARE @FieldId_DotnetVersion INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'DotnetVersion', N'.NET sürümü', N'SingleSelect', 0, 120, NULL, NULL);
    SET @FieldId_DotnetVersion = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_DotnetVersion, N'.NET 8', 1),
        (@FieldId_DotnetVersion, N'.NET 9', 2),
        (@FieldId_DotnetVersion, N'Framework 4.8 (legacy)', 3),
        (@FieldId_DotnetVersion, N'Farketmez', 4);

    DECLARE @FieldId_Logging INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Logging', N'Loglama', N'SingleSelect', 0, 130, NULL, NULL);
    SET @FieldId_Logging = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Logging, N'Yok', 1),
        (@FieldId_Logging, N'Built-in ILogger', 2),
        (@FieldId_Logging, N'Serilog', 3);

    DECLARE @FieldId_TestExpectation INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'TestExpectation', N'Test beklentisi', N'SingleSelect', 0, 140, NULL, NULL);
    SET @FieldId_TestExpectation = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_TestExpectation, N'Yok', 1),
        (@FieldId_TestExpectation, N'Unit test (xUnit/NUnit)', 2),
        (@FieldId_TestExpectation, N'Unit + Integration', 3);

    DECLARE @FieldId_Deployment INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Deployment', N'Deployment', N'SingleSelect', 0, 150, NULL, NULL);
    SET @FieldId_Deployment = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Deployment, N'Local exe', 1),
        (@FieldId_Deployment, N'IIS', 2),
        (@FieldId_Deployment, N'Docker', 3),
        (@FieldId_Deployment, N'Azure', 4),
        (@FieldId_Deployment, N'Windows Service', 5);

    DECLARE @FieldId_ExtraLibraries INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ExtraLibraries', N'Ek kütüphaneler', N'MultiSelect', 1, 160, NULL, NULL);
    SET @FieldId_ExtraLibraries = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_ExtraLibraries, N'AutoMapper', 1),
        (@FieldId_ExtraLibraries, N'MediatR', 2),
        (@FieldId_ExtraLibraries, N'FluentValidation', 3),
        (@FieldId_ExtraLibraries, N'Yok/farketmez', 4);

    DECLARE @FieldId_Languages INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Languages', N'Kullanılacak diller', N'MultiSelect', 1, 170, NULL, NULL);
    SET @FieldId_Languages = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_Languages, N'C#', 1),
        (@FieldId_Languages, N'SQL', 2),
        (@FieldId_Languages, N'JavaScript/TypeScript', 3),
        (@FieldId_Languages, N'PowerShell', 4),
        (@FieldId_Languages, N'Python', 5);

    DECLARE @FieldId_ScriptInterpreter INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ScriptInterpreter', N'Script/otomasyon interpreter''ı', N'SingleSelect', 0, 180, NULL, NULL);
    SET @FieldId_ScriptInterpreter = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, SortOrder) VALUES
        (@FieldId_ScriptInterpreter, N'Yok', 1),
        (@FieldId_ScriptInterpreter, N'PowerShell', 2),
        (@FieldId_ScriptInterpreter, N'Python', 3),
        (@FieldId_ScriptInterpreter, N'Roslyn C# Scripting (CSX)', 4);

END;
GO
