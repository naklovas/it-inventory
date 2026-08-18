IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WizardField' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.WizardField
    (
        FieldId                 INT IDENTITY(1,1) PRIMARY KEY,
        FieldKey                NVARCHAR(50)    NOT NULL UNIQUE,
        Label                   NVARCHAR(200)   NOT NULL,
        LabelEn                 NVARCHAR(200)   NULL,
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
        OptionTextEn NVARCHAR(200)  NULL,
        SortOrder   INT             NOT NULL
    );
END;
GO

-- Ilk kurulumda alanlari/secenekleri bir kere doldurur. Tablo zaten doluysa (daha sonra
-- elle/DB'den duzenlenmis olabilir) hicbir seyi degistirmez.
IF NOT EXISTS (SELECT 1 FROM dbo.WizardField)
BEGIN
    DECLARE @FieldId_AppType INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'AppType', N'Uygulama tipi', N'Application type', N'SingleSelect', 1, 10, NULL, NULL);
    SET @FieldId_AppType = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_AppType, N'Web API', N'Web API', 1),
        (@FieldId_AppType, N'Web App (MVC/Razor)', N'Web App (MVC/Razor)', 2),
        (@FieldId_AppType, N'Blazor (Server/WASM)', N'Blazor (Server/WASM)', 3),
        (@FieldId_AppType, N'WPF', N'WPF', 4),
        (@FieldId_AppType, N'WinForms', N'WinForms', 5),
        (@FieldId_AppType, N'Console/CLI', N'Console/CLI', 6),
        (@FieldId_AppType, N'Windows Service', N'Windows Service', 7),
        (@FieldId_AppType, N'MAUI', N'MAUI', 8);

    DECLARE @FieldId_Domain INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Domain', N'Amaç/domain', N'Purpose/domain', N'SingleSelect', 1, 20, NULL, NULL);
    SET @FieldId_Domain = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Domain, N'CRUD/veri yönetimi', N'CRUD/data management', 1),
        (@FieldId_Domain, N'Envanter-stok takibi', N'Inventory/stock tracking', 2),
        (@FieldId_Domain, N'Muhasebe/finans', N'Accounting/finance', 3),
        (@FieldId_Domain, N'Raporlama/dashboard', N'Reporting/dashboard', 4),
        (@FieldId_Domain, N'Otomasyon/entegrasyon scripti', N'Automation/integration script', 5),
        (@FieldId_Domain, N'Onay/iş akışı sistemi', N'Approval/workflow system', 6);

    DECLARE @FieldId_Scale INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Scale', N'Ölçek', N'Scale', N'SingleSelect', 0, 30, NULL, NULL);
    SET @FieldId_Scale = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Scale, N'Kişisel/tek kullanıcı', N'Personal/single user', 1),
        (@FieldId_Scale, N'Küçük ekip (dahili)', N'Small team (internal)', 2),
        (@FieldId_Scale, N'Kurumsal çok kullanıcılı', N'Enterprise multi-user', 3),
        (@FieldId_Scale, N'İnternete açık', N'Public-facing (internet)', 4);

    DECLARE @FieldId_DataLayer INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'DataLayer', N'Veri katmanı', N'Data layer', N'SingleSelect', 0, 40, NULL, NULL);
    SET @FieldId_DataLayer = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_DataLayer, N'Yok (bellek içi)', N'None (in-memory)', 1),
        (@FieldId_DataLayer, N'SQLite', N'SQLite', 2),
        (@FieldId_DataLayer, N'SQL Server', N'SQL Server', 3),
        (@FieldId_DataLayer, N'PostgreSQL', N'PostgreSQL', 4),
        (@FieldId_DataLayer, N'MySQL', N'MySQL', 5),
        (@FieldId_DataLayer, N'Dosya tabanlı (JSON/Excel/CSV)', N'File-based (JSON/Excel/CSV)', 6);

    DECLARE @FieldId_AccessMethod INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'AccessMethod', N'Veri erişim yöntemi', N'Data access method', N'SingleSelect', 0, 50, N'DataLayer', N'Yok (bellek içi)');
    SET @FieldId_AccessMethod = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_AccessMethod, N'Entity Framework Core', N'Entity Framework Core', 1),
        (@FieldId_AccessMethod, N'Dapper', N'Dapper', 2),
        (@FieldId_AccessMethod, N'ADO.NET (raw)', N'ADO.NET (raw)', 3);

    DECLARE @FieldId_Auth INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Auth', N'Kimlik doğrulama', N'Authentication', N'SingleSelect', 0, 60, NULL, NULL);
    SET @FieldId_Auth = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Auth, N'Yok', N'None', 1),
        (@FieldId_Auth, N'ASP.NET Core Identity', N'ASP.NET Core Identity', 2),
        (@FieldId_Auth, N'JWT', N'JWT', 3),
        (@FieldId_Auth, N'Windows/AD Auth', N'Windows/AD Auth', 4),
        (@FieldId_Auth, N'OAuth (Google/Microsoft)', N'OAuth (Google/Microsoft)', 5),
        (@FieldId_Auth, N'Basit kullanıcı-şifre', N'Simple username/password', 6);

    DECLARE @FieldId_Architecture INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Architecture', N'Mimari', N'Architecture', N'SingleSelect', 0, 70, NULL, NULL);
    SET @FieldId_Architecture = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Architecture, N'Basit tek proje', N'Simple single project', 1),
        (@FieldId_Architecture, N'Katmanlı (N-tier)', N'Layered (N-tier)', 2),
        (@FieldId_Architecture, N'Clean Architecture', N'Clean Architecture', 3),
        (@FieldId_Architecture, N'MVVM (masaüstü)', N'MVVM (desktop)', 4),
        (@FieldId_Architecture, N'Vertical Slice', N'Vertical Slice', 5);

    DECLARE @FieldId_BackendArchitecture INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'BackendArchitecture', N'Backend mimarisi', N'Backend architecture', N'SingleSelect', 0, 80, NULL, NULL);
    SET @FieldId_BackendArchitecture = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_BackendArchitecture, N'Monolit (arayüzle tek proje)', N'Monolith (single project with UI)', 1),
        (@FieldId_BackendArchitecture, N'Ayrı REST API + ayrı frontend', N'Separate REST API + separate frontend', 2),
        (@FieldId_BackendArchitecture, N'Ayrı GraphQL API + frontend', N'Separate GraphQL API + frontend', 3),
        (@FieldId_BackendArchitecture, N'Sadece API (frontend yok)', N'API only (no frontend)', 4);

    DECLARE @FieldId_ApiDocs INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ApiDocs', N'API dokümantasyonu', N'API documentation', N'SingleSelect', 0, 90, N'BackendArchitecture', N'Monolit (arayüzle tek proje)');
    SET @FieldId_ApiDocs = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_ApiDocs, N'Swagger/OpenAPI ekle', N'Add Swagger/OpenAPI', 1),
        (@FieldId_ApiDocs, N'Gerek yok', N'Not needed', 2);

    DECLARE @FieldId_Features INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Features', N'Temel özellikler', N'Core features', N'MultiSelect', 1, 100, NULL, NULL);
    SET @FieldId_Features = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Features, N'Listeleme/filtreleme', N'Listing/filtering', 1),
        (@FieldId_Features, N'CRUD ekranları', N'CRUD screens', 2),
        (@FieldId_Features, N'Excel import/export', N'Excel import/export', 3),
        (@FieldId_Features, N'PDF export', N'PDF export', 4),
        (@FieldId_Features, N'E-posta gönderimi', N'Email sending', 5),
        (@FieldId_Features, N'Zamanlanmış görev', N'Scheduled task', 6),
        (@FieldId_Features, N'Dosya yükleme', N'File upload', 7),
        (@FieldId_Features, N'Arama', N'Search', 8),
        (@FieldId_Features, N'Log/audit trail', N'Log/audit trail', 9),
        (@FieldId_Features, N'Bildirim', N'Notifications', 10),
        (@FieldId_Features, N'3. parti API entegrasyonu', N'3rd-party API integration', 11);

    DECLARE @FieldId_UiStyle INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'UiStyle', N'UI stili', N'UI style', N'SingleSelect', 0, 110, NULL, NULL);
    SET @FieldId_UiStyle = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_UiStyle, N'Minimal', N'Minimal', 1),
        (@FieldId_UiStyle, N'Modern (Bootstrap/MudBlazor/MaterialDesign)', N'Modern (Bootstrap/MudBlazor/MaterialDesign)', 2),
        (@FieldId_UiStyle, N'Kurumsal/tablo ağırlıklı', N'Corporate/table-heavy', 3),
        (@FieldId_UiStyle, N'Dashboard/grafikli', N'Dashboard/chart-heavy', 4);

    DECLARE @FieldId_DotnetVersion INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'DotnetVersion', N'.NET sürümü', N'.NET version', N'SingleSelect', 0, 120, NULL, NULL);
    SET @FieldId_DotnetVersion = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_DotnetVersion, N'.NET 8', N'.NET 8', 1),
        (@FieldId_DotnetVersion, N'.NET 9', N'.NET 9', 2),
        (@FieldId_DotnetVersion, N'Framework 4.8 (legacy)', N'Framework 4.8 (legacy)', 3),
        (@FieldId_DotnetVersion, N'Farketmez', N'Doesn''t matter', 4);

    DECLARE @FieldId_Logging INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Logging', N'Loglama', N'Logging', N'SingleSelect', 0, 130, NULL, NULL);
    SET @FieldId_Logging = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Logging, N'Yok', N'None', 1),
        (@FieldId_Logging, N'Built-in ILogger', N'Built-in ILogger', 2),
        (@FieldId_Logging, N'Serilog', N'Serilog', 3);

    DECLARE @FieldId_TestExpectation INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'TestExpectation', N'Test beklentisi', N'Testing expectations', N'SingleSelect', 0, 140, NULL, NULL);
    SET @FieldId_TestExpectation = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_TestExpectation, N'Yok', N'None', 1),
        (@FieldId_TestExpectation, N'Unit test (xUnit/NUnit)', N'Unit tests (xUnit/NUnit)', 2),
        (@FieldId_TestExpectation, N'Unit + Integration', N'Unit + Integration', 3);

    DECLARE @FieldId_Deployment INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Deployment', N'Deployment', N'Deployment', N'SingleSelect', 0, 150, NULL, NULL);
    SET @FieldId_Deployment = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Deployment, N'Local exe', N'Local exe', 1),
        (@FieldId_Deployment, N'IIS', N'IIS', 2),
        (@FieldId_Deployment, N'Docker', N'Docker', 3),
        (@FieldId_Deployment, N'Azure', N'Azure', 4),
        (@FieldId_Deployment, N'Windows Service', N'Windows Service', 5);

    DECLARE @FieldId_ExtraLibraries INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ExtraLibraries', N'Ek kütüphaneler', N'Additional libraries', N'MultiSelect', 1, 160, NULL, NULL);
    SET @FieldId_ExtraLibraries = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_ExtraLibraries, N'AutoMapper', N'AutoMapper', 1),
        (@FieldId_ExtraLibraries, N'MediatR', N'MediatR', 2),
        (@FieldId_ExtraLibraries, N'FluentValidation', N'FluentValidation', 3),
        (@FieldId_ExtraLibraries, N'Yok/farketmez', N'None/doesn''t matter', 4);

    DECLARE @FieldId_Languages INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Languages', N'Kullanılacak diller', N'Languages to use', N'MultiSelect', 1, 170, NULL, NULL);
    SET @FieldId_Languages = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_Languages, N'C#', N'C#', 1),
        (@FieldId_Languages, N'SQL', N'SQL', 2),
        (@FieldId_Languages, N'JavaScript/TypeScript', N'JavaScript/TypeScript', 3),
        (@FieldId_Languages, N'PowerShell', N'PowerShell', 4),
        (@FieldId_Languages, N'Python', N'Python', 5);

    DECLARE @FieldId_ScriptInterpreter INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ScriptInterpreter', N'Script/otomasyon interpreter''ı', N'Scripting/automation interpreter', N'SingleSelect', 0, 180, NULL, NULL);
    SET @FieldId_ScriptInterpreter = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, SortOrder) VALUES
        (@FieldId_ScriptInterpreter, N'Yok', N'None', 1),
        (@FieldId_ScriptInterpreter, N'PowerShell', N'PowerShell', 2),
        (@FieldId_ScriptInterpreter, N'Python', N'Python', 3),
        (@FieldId_ScriptInterpreter, N'Roslyn C# Scripting (CSX)', N'Roslyn C# Scripting (CSX)', 4);

END;
GO
