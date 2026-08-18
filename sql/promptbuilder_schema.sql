IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WizardField' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.WizardField
    (
        FieldId                 INT IDENTITY(1,1) PRIMARY KEY,
        FieldKey                NVARCHAR(50)    NOT NULL UNIQUE,
        Label                   NVARCHAR(200)   NOT NULL,
        LabelEn                 NVARCHAR(200)   NULL,
        Help                    NVARCHAR(500)   NULL, -- alanin ne ise yaradigini/ne zaman kullanilacagini aciklar
        HelpEn                  NVARCHAR(500)   NULL,
        FieldType               NVARCHAR(20)    NOT NULL, -- 'SingleSelect' | 'MultiSelect'
        AllowOther              BIT             NOT NULL DEFAULT 1,
        AllowItemNotes          BIT             NOT NULL DEFAULT 0, -- secilen her secenege serbest not eklenebilsin mi
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
        OptionId        INT IDENTITY(1,1) PRIMARY KEY,
        FieldId         INT             NOT NULL REFERENCES dbo.WizardField(FieldId) ON DELETE CASCADE,
        OptionText      NVARCHAR(200)   NOT NULL,
        OptionTextEn    NVARCHAR(200)   NULL,
        OptionHelp      NVARCHAR(300)   NULL, -- secenegin ne oldugunu/ne zaman tercih edilecegini aciklar
        OptionHelpEn    NVARCHAR(300)   NULL,
        SortOrder       INT             NOT NULL
    );
END;
GO

-- Ilk kurulumda alanlari/secenekleri bir kere doldurur. Tablo zaten doluysa (daha sonra
-- elle/DB'den duzenlenmis olabilir) hicbir seyi degistirmez.
IF NOT EXISTS (SELECT 1 FROM dbo.WizardField)
BEGIN
    DECLARE @FieldId_AppType INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'AppType', N'Uygulama tipi', N'Application type', N'Uygulamanın nasıl çalışacağını (web sayfası, masaüstü penceresi, komut satırı vb.) belirler. Kullanıcıların uygulamayla nasıl etkileşime gireceğini düşünüp seçin.', N'Determines how the application runs (web page, desktop window, command line, etc.). Pick based on how users will interact with it.', N'SingleSelect', 1, 0, 10, NULL, NULL);
    SET @FieldId_AppType = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_AppType, N'Web API', N'Web API', N'Sadece HTTP uç noktaları sunan, arayüzü olmayan backend; başka bir frontend veya mobil uygulama tarafından tüketilir.', N'A backend that only exposes HTTP endpoints, no UI; consumed by a separate frontend or mobile app.', 1),
        (@FieldId_AppType, N'Web App (MVC/Razor)', N'Web App (MVC/Razor)', N'Sunucu tarafında render edilen klasik web uygulaması; SEO ve basit dağıtım için iyi.', N'A classic server-rendered web app; good for SEO and simple deployment.', 2),
        (@FieldId_AppType, N'Blazor (Server/WASM)', N'Blazor (Server/WASM)', N'C# ile zengin, etkileşimli web arayüzü; JavaScript yazmadan SPA benzeri deneyim istiyorsanız uygun.', N'A rich, interactive web UI written in C#; good if you want an SPA-like experience without writing JavaScript.', 3),
        (@FieldId_AppType, N'WPF', N'WPF', N'Zengin, özelleştirilebilir masaüstü arayüzü gerektiren Windows uygulamaları için.', N'For Windows desktop apps that need a rich, customizable UI.', 4),
        (@FieldId_AppType, N'WinForms', N'WinForms', N'Hızlıca kurulan, basit form tabanlı klasik Windows masaüstü uygulamaları için.', N'For quick, simple form-based classic Windows desktop apps.', 5),
        (@FieldId_AppType, N'Console/CLI', N'Console/CLI', N'Arayüzsüz, komut satırından çalışan araçlar/scriptler için; otomasyon ve zamanlanmış görevlere uygun.', N'For UI-less tools/scripts run from the command line; good for automation and scheduled tasks.', 6),
        (@FieldId_AppType, N'Windows Service', N'Windows Service', N'Arka planda, kullanıcı oturumu olmadan sürekli çalışması gereken işler için.', N'For work that must run continuously in the background, without a user session.', 7),
        (@FieldId_AppType, N'MAUI', N'MAUI', N'Aynı kod tabanından Windows, Android, iOS gibi birden fazla platforma çıkmak istiyorsanız.', N'If you want to target Windows, Android, iOS, etc. from a single codebase.', 8);

    DECLARE @FieldId_Domain INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Domain', N'Amaç/domain', N'Purpose/domain', N'Uygulamanın hangi iş problemini çözdüğünü tanımlar; LLM''e doğru veri modelini ve ekranları önermesi için bağlam verir.', N'Describes what business problem the app solves; gives the LLM context to suggest the right data model and screens.', N'SingleSelect', 1, 0, 20, NULL, NULL);
    SET @FieldId_Domain = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Domain, N'CRUD/veri yönetimi', N'CRUD/data management', N'Kayıt ekleme/düzenleme/silme/listeleme ağırlıklı, klasik veri yönetim uygulamaları.', N'Classic data-management apps centered on adding/editing/deleting/listing records.', 1),
        (@FieldId_Domain, N'Envanter-stok takibi', N'Inventory/stock tracking', N'Ürün/malzeme miktarlarını, giriş-çıkışları ve konumları izlemek için.', N'For tracking product/material quantities, movements, and locations.', 2),
        (@FieldId_Domain, N'Muhasebe/finans', N'Accounting/finance', N'Fatura, bütçe, ödeme gibi finansal kayıtları işleyen uygulamalar için.', N'For apps handling financial records like invoices, budgets, and payments.', 3),
        (@FieldId_Domain, N'Raporlama/dashboard', N'Reporting/dashboard', N'Var olan veriyi özetleyip görselleştirmek, KPI takibi için.', N'For summarizing and visualizing existing data, KPI tracking.', 4),
        (@FieldId_Domain, N'Otomasyon/entegrasyon scripti', N'Automation/integration script', N'Sistemler arası veri aktarımı veya tekrarlayan görevleri otomatikleştirmek için.', N'For moving data between systems or automating repetitive tasks.', 5),
        (@FieldId_Domain, N'Onay/iş akışı sistemi', N'Approval/workflow system', N'Bir talebin birden fazla aşamadan/onaydan geçtiği süreçleri yönetmek için.', N'For managing processes where a request passes through multiple stages/approvals.', 6);

    DECLARE @FieldId_Scale INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Scale', N'Ölçek', N'Scale', N'Kaç kullanıcının aynı anda kullanacağını belirtir; bu, auth, performans ve altyapı kararlarını etkiler.', N'Indicates how many users will use it concurrently; this affects auth, performance, and infrastructure decisions.', N'SingleSelect', 0, 0, 30, NULL, NULL);
    SET @FieldId_Scale = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Scale, N'Kişisel/tek kullanıcı', N'Personal/single user', N'Sadece sizin kullanacağınız, basit tutulabilecek araçlar için; auth/ölçeklenebilirlik önemsiz.', N'For tools only you will use; auth/scalability don''t matter much.', 1),
        (@FieldId_Scale, N'Küçük ekip (dahili)', N'Small team (internal)', N'Şirket içinde birkaç kişinin kullanacağı araçlar; basit auth yeterli olabilir.', N'For tools used by a few people internally; simple auth may be enough.', 2),
        (@FieldId_Scale, N'Kurumsal çok kullanıcılı', N'Enterprise multi-user', N'Çok sayıda kullanıcı ve rol/izin yönetimi gerektiren kurumsal uygulamalar.', N'For enterprise apps with many users and role/permission management needs.', 3),
        (@FieldId_Scale, N'İnternete açık', N'Public-facing (internet)', N'Herkesin erişebileceği; güvenlik, ölçeklenebilirlik ve saldırı yüzeyi öncelikli düşünülmeli.', N'Accessible to anyone; security, scalability, and attack surface should be top priorities.', 4);

    DECLARE @FieldId_DataLayer INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'DataLayer', N'Veri katmanı', N'Data layer', N'Verinin nerede saklanacağını belirler. Basit/tek kullanıcılı araçlarda ''Yok'' ya da SQLite yeterli olabilir, kurumsal kullanımda SQL Server/PostgreSQL tercih edin.', N'Determines where data is stored. For simple/single-user tools, ''None'' or SQLite may be enough; for enterprise use, prefer SQL Server/PostgreSQL.', N'SingleSelect', 0, 0, 40, NULL, NULL);
    SET @FieldId_DataLayer = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_DataLayer, N'Yok (bellek içi)', N'None (in-memory)', N'Veri kalıcı olmayacaksa veya prototip aşamasındaysanız.', N'If data doesn''t need to persist, or you''re prototyping.', 1),
        (@FieldId_DataLayer, N'SQLite', N'SQLite', N'Kurulum gerektirmeyen, tek dosyalı hafif veritabanı; küçük/masaüstü uygulamalar için ideal.', N'A lightweight, single-file database needing no setup; ideal for small/desktop apps.', 2),
        (@FieldId_DataLayer, N'SQL Server', N'SQL Server', N'Kurumsal, Windows/.NET ekosistemiyle uyumlu, güçlü bir ilişkisel veritabanı.', N'A robust relational database, well-integrated with the Windows/.NET ecosystem, common in enterprises.', 3),
        (@FieldId_DataLayer, N'PostgreSQL', N'PostgreSQL', N'Açık kaynak, gelişmiş özellikli, platform bağımsız bir ilişkisel veritabanı.', N'An open-source, feature-rich, cross-platform relational database.', 4),
        (@FieldId_DataLayer, N'MySQL', N'MySQL', N'Yaygın kullanılan, açık kaynak bir ilişkisel veritabanı; web uygulamalarında sık tercih edilir.', N'A widely used open-source relational database, common in web apps.', 5),
        (@FieldId_DataLayer, N'Dosya tabanlı (JSON/Excel/CSV)', N'File-based (JSON/Excel/CSV)', N'Basit, taşınabilir veri saklama; gerçek bir veritabanı gerekmeyen küçük araçlar için.', N'Simple, portable data storage; for small tools that don''t need a real database.', 6);

    DECLARE @FieldId_AccessMethod INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'AccessMethod', N'Veri erişim yöntemi', N'Data access method', N'Veritabanına nasıl erişileceğini belirler; hız/kontrol ile geliştirme hızı arasındaki tercihi yansıtır.', N'Determines how the database is accessed; reflects the trade-off between speed/control and development speed.', N'SingleSelect', 0, 0, 50, N'DataLayer', N'Yok (bellek içi)');
    SET @FieldId_AccessMethod = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_AccessMethod, N'Entity Framework Core', N'Entity Framework Core', N'Nesne tabanlı, hızlı geliştirme sağlayan ORM; SQL yazmadan veri erişimi ister.', N'An object-based ORM for fast development; use if you want data access without writing SQL.', 1),
        (@FieldId_AccessMethod, N'Dapper', N'Dapper', N'Hafif, hızlı, SQL''e daha yakın micro-ORM; performans önemliyse tercih edilir.', N'A lightweight, fast micro-ORM closer to raw SQL; preferred when performance matters.', 2),
        (@FieldId_AccessMethod, N'ADO.NET (raw)', N'ADO.NET (raw)', N'En düşük seviyeli, tam kontrol sağlayan erişim; ekstra bağımlılık istemiyorsanız.', N'The lowest-level access with full control; use if you don''t want an extra dependency.', 3);

    DECLARE @FieldId_Auth INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Auth', N'Kimlik doğrulama', N'Authentication', N'Uygulamaya kimlerin nasıl giriş yapacağını belirler. Kurumsal ortamda Windows/AD ya da JWT, herkese açık uygulamalarda OAuth tercih edilir.', N'Determines who can log in and how. Prefer Windows/AD or JWT in corporate settings, OAuth for public-facing apps.', N'SingleSelect', 0, 0, 60, NULL, NULL);
    SET @FieldId_Auth = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Auth, N'Yok', N'None', N'Uygulama kimlik doğrulama gerektirmiyorsa (örn. tamamen dahili, herkese açık salt-okunur araç).', N'If the app doesn''t need authentication (e.g. fully internal, public read-only tool).', 1),
        (@FieldId_Auth, N'ASP.NET Core Identity', N'ASP.NET Core Identity', N'Kullanıcı kaydı, şifre, rol yönetimini kendi veritabanınızda tutmak istiyorsanız.', N'If you want to manage user registration, passwords, and roles in your own database.', 2),
        (@FieldId_Auth, N'JWT', N'JWT', N'Stateless API''ler için token tabanlı kimlik doğrulama; mobil/SPA client''larla iyi çalışır.', N'Token-based auth for stateless APIs; works well with mobile/SPA clients.', 3),
        (@FieldId_Auth, N'Windows/AD Auth', N'Windows/AD Auth', N'Şirket içi kullanıcıların zaten Windows/AD hesaplarıyla otomatik giriş yapmasını istiyorsanız.', N'If internal users should log in automatically with their existing Windows/AD accounts.', 4),
        (@FieldId_Auth, N'OAuth (Google/Microsoft)', N'OAuth (Google/Microsoft)', N'Kullanıcıların mevcut Google/Microsoft hesaplarıyla giriş yapmasını istiyorsanız.', N'If users should log in with their existing Google/Microsoft accounts.', 5),
        (@FieldId_Auth, N'Basit kullanıcı-şifre', N'Simple username/password', N'Hızlı, minimal bir giriş mekanizması yeterliyse (üretim için önerilmez).', N'If a quick, minimal login mechanism is enough (not recommended for production).', 6);

    DECLARE @FieldId_Architecture INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Architecture', N'Mimari', N'Architecture', N'Kod tabanının nasıl organize edileceğini belirler. Küçük araçlarda ''Basit tek proje'', büyüyecek/uzun ömürlü projelerde Clean Architecture ya da katmanlı mimari tercih edilir.', N'Determines how the codebase is organized. Use ''Simple single project'' for small tools; prefer Clean Architecture or layered architecture for larger, long-lived projects.', N'SingleSelect', 0, 0, 70, NULL, NULL);
    SET @FieldId_Architecture = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Architecture, N'Basit tek proje', N'Simple single project', N'Küçük, kısa ömürlü araçlar için; katmanlara ayırmaya gerek yok.', N'For small, short-lived tools; no need to split into layers.', 1),
        (@FieldId_Architecture, N'Katmanlı (N-tier)', N'Layered (N-tier)', N'Sunum/iş mantığı/veri erişimini ayrı katmanlara bölen klasik, anlaşılır yapı.', N'A classic, easy-to-understand structure splitting presentation/business logic/data access into layers.', 2),
        (@FieldId_Architecture, N'Clean Architecture', N'Clean Architecture', N'İş mantığını framework ve veritabanından bağımsız tutan, test edilebilirliği yüksek yapı; büyük/uzun ömürlü projeler için.', N'Keeps business logic independent of framework/database, highly testable; for large, long-lived projects.', 3),
        (@FieldId_Architecture, N'MVVM (masaüstü)', N'MVVM (desktop)', N'WPF/MAUI gibi masaüstü UI''larda arayüz ile mantığı ayırmak için standart desen.', N'The standard pattern for separating UI from logic in desktop UIs like WPF/MAUI.', 4),
        (@FieldId_Architecture, N'Vertical Slice', N'Vertical Slice', N'Katman yerine özellik (feature) bazlı organize eder; her özelliğin kodu bir arada durur.', N'Organizes code by feature instead of by layer; each feature''s code stays together.', 5);

    DECLARE @FieldId_BackendArchitecture INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'BackendArchitecture', N'Backend mimarisi', N'Backend architecture', N'Arayüz ile iş mantığının aynı projede mi yoksa ayrı bir API üzerinden mi haberleşeceğini belirler. Birden fazla client (web+mobil gibi) planlanıyorsa ayrı API tercih edilir.', N'Determines whether the UI and business logic live in one project or talk through a separate API. Prefer a separate API if multiple clients (web + mobile, etc.) are planned.', N'SingleSelect', 0, 0, 80, NULL, NULL);
    SET @FieldId_BackendArchitecture = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_BackendArchitecture, N'Monolit (arayüzle tek proje)', N'Monolith (single project with UI)', N'Basitlik ve hızlı geliştirme öncelikliyse; tek client varsa yeterli.', N'When simplicity and fast development matter most; fine if there''s only one client.', 1),
        (@FieldId_BackendArchitecture, N'Ayrı REST API + ayrı frontend', N'Separate REST API + separate frontend', N'Birden fazla client (web, mobil) aynı backend''i kullanacaksa.', N'When multiple clients (web, mobile) will share the same backend.', 2),
        (@FieldId_BackendArchitecture, N'Ayrı GraphQL API + frontend', N'Separate GraphQL API + frontend', N'Client''ların ihtiyaç duyduğu veriyi esnek şekilde sorgulaması gerekiyorsa.', N'When clients need to flexibly query exactly the data they need.', 3),
        (@FieldId_BackendArchitecture, N'Sadece API (frontend yok)', N'API only (no frontend)', N'Arayüz başka bir ekip/proje tarafından yapılacaksa veya sadece entegrasyon API''si gerekiyorsa.', N'When the UI will be built separately, or only an integration API is needed.', 4);

    DECLARE @FieldId_ApiDocs INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ApiDocs', N'API dokümantasyonu', N'API documentation', N'Ayrı bir API varsa, onun uç noktalarının Swagger/OpenAPI ile otomatik belgelenip belgelenmeyeceğini belirler. Başka ekipler/clientlar API''yi kullanacaksa önerilir.', N'If there''s a separate API, determines whether its endpoints are auto-documented with Swagger/OpenAPI. Recommended if other teams/clients will consume the API.', N'SingleSelect', 0, 0, 90, N'BackendArchitecture', N'Monolit (arayüzle tek proje)');
    SET @FieldId_ApiDocs = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_ApiDocs, N'Swagger/OpenAPI ekle', N'Add Swagger/OpenAPI', N'API''yi başka geliştiriciler/ekipler kullanacaksa, otomatik/interaktif dokümantasyon için önerilir.', N'Recommended when other developers/teams will consume the API, for automatic/interactive docs.', 1),
        (@FieldId_ApiDocs, N'Gerek yok', N'Not needed', N'API sadece sizin kontrolünüzdeki tek bir client tarafından kullanılacaksa.', N'If the API is only used by a single client you control.', 2);

    DECLARE @FieldId_Features INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Features', N'Temel özellikler', N'Core features', N'Uygulamada olmasını istediğiniz somut yetenekleri seçin; her biri için gerekirse aşağıda kısa bir açıklama/gerekçe ekleyebilirsiniz.', N'Pick the concrete capabilities you want in the app; you can add a short note/justification for each below if needed.', N'MultiSelect', 1, 1, 100, NULL, NULL);
    SET @FieldId_Features = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Features, N'Listeleme/filtreleme', N'Listing/filtering', N'Kayıtları tablo halinde görüntüleyip arama/filtre ile daraltma.', N'Displaying records in a table with search/filter narrowing.', 1),
        (@FieldId_Features, N'CRUD ekranları', N'CRUD screens', N'Kayıt ekleme, düzenleme, silme ve görüntüleme formları.', N'Forms for adding, editing, deleting, and viewing records.', 2),
        (@FieldId_Features, N'Excel import/export', N'Excel import/export', N'Veriyi Excel''e aktarma veya Excel''den içeri alma.', N'Exporting data to Excel or importing it from Excel.', 3),
        (@FieldId_Features, N'PDF export', N'PDF export', N'Kayıt/raporu PDF olarak indirilebilir hale getirme.', N'Making a record/report downloadable as PDF.', 4),
        (@FieldId_Features, N'E-posta gönderimi', N'Email sending', N'Bildirim, onay ya da rapor gibi e-postaların uygulamadan gönderilmesi.', N'Sending emails like notifications, confirmations, or reports from the app.', 5),
        (@FieldId_Features, N'Zamanlanmış görev', N'Scheduled task', N'Belirli aralıklarla otomatik çalışan arka plan işleri (örn. gece raporu).', N'Background jobs that run automatically on a schedule (e.g. nightly report).', 6),
        (@FieldId_Features, N'Dosya yükleme', N'File upload', N'Kullanıcının dosya/ek yükleyebilmesi (resim, belge vb.).', N'Letting users upload files/attachments (images, documents, etc.).', 7),
        (@FieldId_Features, N'Arama', N'Search', N'Kayıtlar arasında serbest metin veya gelişmiş arama.', N'Free-text or advanced search across records.', 8),
        (@FieldId_Features, N'Log/audit trail', N'Log/audit trail', N'Kimin ne zaman ne değiştirdiğinin kaydını tutma; denetim/izlenebilirlik için.', N'Recording who changed what and when; for auditing/traceability.', 9),
        (@FieldId_Features, N'Bildirim', N'Notifications', N'Uygulama içi veya push bildirimleriyle kullanıcıyı bilgilendirme.', N'Informing users via in-app or push notifications.', 10),
        (@FieldId_Features, N'3. parti API entegrasyonu', N'3rd-party API integration', N'Harici bir servisle (ödeme, harita, SMS vb.) konuşma.', N'Talking to an external service (payment, maps, SMS, etc.).', 11);

    DECLARE @FieldId_UiStyle INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'UiStyle', N'UI stili', N'UI style', N'Arayüzün görsel ağırlığını belirler; kullanıcı kitlesine ve markanıza göre seçin.', N'Determines the visual weight of the UI; choose based on your audience and branding.', N'SingleSelect', 0, 0, 110, NULL, NULL);
    SET @FieldId_UiStyle = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_UiStyle, N'Minimal', N'Minimal', N'Sade, dikkat dağıtmayan; işlevsellik öncelikliyse.', N'Plain, distraction-free; when functionality is the priority.', 1),
        (@FieldId_UiStyle, N'Modern (Bootstrap/MudBlazor/MaterialDesign)', N'Modern (Bootstrap/MudBlazor/MaterialDesign)', N'Hazır bileşen kütüphaneleriyle çağdaş, cilalı bir görünüm.', N'A contemporary, polished look using ready-made component libraries.', 2),
        (@FieldId_UiStyle, N'Kurumsal/tablo ağırlıklı', N'Corporate/table-heavy', N'Yoğun veri gösteren, tablo/form odaklı iç kullanım araçları.', N'Data-dense, table/form-focused internal tools.', 3),
        (@FieldId_UiStyle, N'Dashboard/grafikli', N'Dashboard/chart-heavy', N'Özet metrikleri grafik/kart olarak öne çıkaran görsel ağırlıklı arayüz.', N'A visually driven UI highlighting summary metrics as charts/cards.', 4);

    DECLARE @FieldId_DotnetVersion INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'DotnetVersion', N'.NET sürümü', N'.NET version', N'Hedeflenecek .NET sürümünü belirler. Yeni projelerde en güncel LTS/aktif sürüm önerilir; eski sistemlerle uyumluluk gerekiyorsa Framework 4.8 seçilebilir.', N'Determines the target .NET version. For new projects, the latest LTS/active version is recommended; pick Framework 4.8 only if compatibility with legacy systems is required.', N'SingleSelect', 0, 0, 120, NULL, NULL);
    SET @FieldId_DotnetVersion = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_DotnetVersion, N'.NET 8', N'.NET 8', N'Güncel LTS (uzun destekli) sürüm; çoğu yeni proje için önerilir.', N'The current LTS (long-term support) release; recommended for most new projects.', 1),
        (@FieldId_DotnetVersion, N'.NET 9', N'.NET 9', N'En güncel özellikleri istiyorsanız; LTS değildir, destek süresi daha kısadır.', N'If you want the latest features; not LTS, shorter support window.', 2),
        (@FieldId_DotnetVersion, N'Framework 4.8 (legacy)', N'Framework 4.8 (legacy)', N'Sadece eski/legacy Windows-only bileşenlerle uyumluluk gerekiyorsa.', N'Only if compatibility with legacy Windows-only components is required.', 3),
        (@FieldId_DotnetVersion, N'Farketmez', N'Doesn''t matter', N'Sürüm kararını geliştiriciye/LLM''e bırakmak istiyorsanız.', N'If you want to leave the version decision to the developer/LLM.', 4);

    DECLARE @FieldId_Logging INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Logging', N'Loglama', N'Logging', N'Çalışma zamanı olaylarının nasıl kaydedileceğini belirler. Basit araçlarda built-in ILogger yeterlidir; yapılandırılmış/dosyaya yazan loglama gerekiyorsa Serilog tercih edin.', N'Determines how runtime events are recorded. Built-in ILogger is enough for simple tools; prefer Serilog if you need structured/file-based logging.', N'SingleSelect', 0, 0, 130, NULL, NULL);
    SET @FieldId_Logging = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Logging, N'Yok', N'None', N'Çok kısa ömürlü/basit bir araç, loglamaya gerek yoksa.', N'For a very short-lived/simple tool that doesn''t need logging.', 1),
        (@FieldId_Logging, N'Built-in ILogger', N'Built-in ILogger', N'Ekstra bağımlılık istemeyen, .NET''in kendi loglama altyapısı.', N'.NET''s own logging infrastructure, when you don''t want an extra dependency.', 2),
        (@FieldId_Logging, N'Serilog', N'Serilog', N'Yapılandırılmış, dosyaya/harici sistemlere (Seq, Elastic vb.) yazabilen gelişmiş loglama.', N'Structured logging that can write to files/external systems (Seq, Elastic, etc.).', 3);

    DECLARE @FieldId_TestExpectation INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'TestExpectation', N'Test beklentisi', N'Testing expectations', N'Ne kadar otomatik test isteneceğini belirler. Prototip/tek seferlik araçlarda ''Yok'' olabilir, üretime gidecek projelerde en az unit test önerilir.', N'Determines how much automated testing is expected. ''None'' may be fine for prototypes/one-off tools; at least unit tests are recommended for production-bound projects.', N'SingleSelect', 0, 0, 140, NULL, NULL);
    SET @FieldId_TestExpectation = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_TestExpectation, N'Yok', N'None', N'Hızlı bir prototip/tek seferlik araç, test yatırımı gerekmiyorsa.', N'For a quick prototype/one-off tool that doesn''t need test investment.', 1),
        (@FieldId_TestExpectation, N'Unit test (xUnit/NUnit)', N'Unit tests (xUnit/NUnit)', N'İş mantığının izole birim testlerle doğrulanmasını istiyorsanız.', N'If you want business logic verified with isolated unit tests.', 2),
        (@FieldId_TestExpectation, N'Unit + Integration', N'Unit + Integration', N'Birim testlerin yanında veritabanı/API gibi entegrasyon noktalarının da test edilmesini istiyorsanız.', N'If you also want integration points like the database/API tested, alongside unit tests.', 3);

    DECLARE @FieldId_Deployment INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Deployment', N'Deployment', N'Deployment', N'Uygulamanın nerede/nasıl çalıştırılacağını belirler; bu, konfigürasyon ve paketleme kararlarını etkiler.', N'Determines where/how the app will run; this affects configuration and packaging decisions.', N'SingleSelect', 0, 0, 150, NULL, NULL);
    SET @FieldId_Deployment = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Deployment, N'Local exe', N'Local exe', N'Tek makinede, kurulumsuz çalışacak masaüstü/konsol araçları için.', N'For desktop/console tools that run on a single machine, no install.', 1),
        (@FieldId_Deployment, N'IIS', N'IIS', N'Windows Server üzerinde klasik web uygulaması barındırma.', N'Classic web app hosting on Windows Server.', 2),
        (@FieldId_Deployment, N'Docker', N'Docker', N'Taşınabilir, ortamdan bağımsız container olarak dağıtmak istiyorsanız.', N'If you want a portable, environment-independent container deployment.', 3),
        (@FieldId_Deployment, N'Azure', N'Azure', N'Bulutta, Microsoft''un yönetilen servisleriyle barındırmak istiyorsanız.', N'If you want cloud hosting using Microsoft''s managed services.', 4),
        (@FieldId_Deployment, N'Windows Service', N'Windows Service', N'Sunucuda arka planda, kullanıcı oturumu olmadan sürekli çalışacaksa.', N'If it needs to run continuously in the background on a server, without a user session.', 5);

    DECLARE @FieldId_ExtraLibraries INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ExtraLibraries', N'Ek kütüphaneler', N'Additional libraries', N'Sık kullanılan yardımcı kütüphanelerden hangilerinin dahil edilmesini istediğinizi belirtin; kod tekrarını azaltıp standart pratikleri getirirler.', N'Specify which common helper libraries you want included; they reduce boilerplate and bring in standard practices.', N'MultiSelect', 1, 0, 160, NULL, NULL);
    SET @FieldId_ExtraLibraries = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_ExtraLibraries, N'AutoMapper', N'AutoMapper', N'Nesneler arası (örn. entity → DTO) dönüşümleri elle yazmak istemiyorsanız.', N'If you don''t want to hand-write object-to-object (e.g. entity → DTO) mappings.', 1),
        (@FieldId_ExtraLibraries, N'MediatR', N'MediatR', N'İstek/komut işleme akışını (CQRS benzeri) merkezi bir aracıdan geçirmek istiyorsanız.', N'If you want request/command handling routed through a central mediator (CQRS-like).', 2),
        (@FieldId_ExtraLibraries, N'FluentValidation', N'FluentValidation', N'Karmaşık doğrulama kurallarını okunabilir, ayrı sınıflarda tanımlamak istiyorsanız.', N'If you want complex validation rules defined readably in separate classes.', 3),
        (@FieldId_ExtraLibraries, N'Yok/farketmez', N'None/doesn''t matter', N'Ek kütüphane istemiyorsanız veya kararı LLM''e bırakıyorsanız.', N'If you don''t want extra libraries, or you''re leaving the choice to the LLM.', 4);

    DECLARE @FieldId_Languages INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'Languages', N'Kullanılacak diller', N'Languages to use', N'Projede C# dışında hangi dillerin (SQL script''leri, frontend JS/TS, otomasyon scriptleri vb.) kullanılacağını belirtir.', N'Specifies which languages besides C# will be used in the project (SQL scripts, frontend JS/TS, automation scripts, etc.).', N'MultiSelect', 1, 0, 170, NULL, NULL);
    SET @FieldId_Languages = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_Languages, N'C#', N'C#', N'Ana uygulama dili; neredeyse her proje için gereklidir.', N'The main application language; needed for almost every project.', 1),
        (@FieldId_Languages, N'SQL', N'SQL', N'Veritabanı sorguları/prosedürleri elle yazılacaksa.', N'If database queries/procedures will be hand-written.', 2),
        (@FieldId_Languages, N'JavaScript/TypeScript', N'JavaScript/TypeScript', N'Zengin bir frontend (SPA, özel JS bileşenleri) gerekiyorsa.', N'If a rich frontend (SPA, custom JS components) is needed.', 3),
        (@FieldId_Languages, N'PowerShell', N'PowerShell', N'Windows''a özgü otomasyon/betik görevleri gerekiyorsa.', N'If Windows-specific automation/scripting tasks are needed.', 4),
        (@FieldId_Languages, N'Python', N'Python', N'Veri işleme, otomasyon ya da mevcut Python araçlarıyla entegrasyon gerekiyorsa.', N'If data processing, automation, or integration with existing Python tools is needed.', 5);

    DECLARE @FieldId_ScriptInterpreter INT;
    INSERT INTO dbo.WizardField (FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther, AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue)
    VALUES (N'ScriptInterpreter', N'Script/otomasyon interpreter''ı', N'Scripting/automation interpreter', N'Uygulamanın içine gömülü/çalışma zamanında çalıştırılan bir script motoru gerekip gerekmediğini belirler (örn. kullanıcı tanımlı kurallar, otomasyon adımları).', N'Determines whether the app needs an embedded/runtime script engine (e.g. for user-defined rules, automation steps).', N'SingleSelect', 0, 0, 180, NULL, NULL);
    SET @FieldId_ScriptInterpreter = SCOPE_IDENTITY();
    INSERT INTO dbo.WizardOption (FieldId, OptionText, OptionTextEn, OptionHelp, OptionHelpEn, SortOrder) VALUES
        (@FieldId_ScriptInterpreter, N'Yok', N'None', N'Uygulamanın çalışma zamanında script çalıştırması gerekmiyorsa.', N'If the app doesn''t need to run scripts at runtime.', 1),
        (@FieldId_ScriptInterpreter, N'PowerShell', N'PowerShell', N'Windows otomasyon script''lerini uygulama içinden tetiklemek/çalıştırmak istiyorsanız.', N'If you want to trigger/run Windows automation scripts from within the app.', 2),
        (@FieldId_ScriptInterpreter, N'Python', N'Python', N'Python script''lerini uygulama içinden çalıştırmak istiyorsanız (örn. veri işleme).', N'If you want to run Python scripts from within the app (e.g. data processing).', 3),
        (@FieldId_ScriptInterpreter, N'Roslyn C# Scripting (CSX)', N'Roslyn C# Scripting (CSX)', N'Kullanıcıların çalışma zamanında C# kod parçacıkları/kurallar tanımlamasına izin vermek istiyorsanız.', N'If you want to let users define C# code snippets/rules at runtime.', 4);

END;
GO
