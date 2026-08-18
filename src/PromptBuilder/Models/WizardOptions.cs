namespace PromptBuilder.Models;

public static class WizardOptions
{
    public static readonly string[] AppType =
    [
        "Web API", "Web App (MVC/Razor)", "Blazor (Server/WASM)", "WPF",
        "WinForms", "Console/CLI", "Windows Service", "MAUI"
    ];

    public static readonly string[] Domain =
    [
        "CRUD/veri yönetimi", "Envanter-stok takibi", "Muhasebe/finans",
        "Raporlama/dashboard", "Otomasyon/entegrasyon scripti", "Onay/iş akışı sistemi"
    ];

    public static readonly string[] Scale =
    [
        "Kişisel/tek kullanıcı", "Küçük ekip (dahili)", "Kurumsal çok kullanıcılı", "İnternete açık"
    ];

    public static readonly string[] DataLayer =
    [
        "Yok (bellek içi)", "SQLite", "SQL Server", "PostgreSQL", "MySQL", "Dosya tabanlı (JSON/Excel/CSV)"
    ];

    public static readonly string[] AccessMethod =
    [
        "Entity Framework Core", "Dapper", "ADO.NET (raw)"
    ];

    public static readonly string[] Auth =
    [
        "Yok", "ASP.NET Core Identity", "JWT", "Windows/AD Auth",
        "OAuth (Google/Microsoft)", "Basit kullanıcı-şifre"
    ];

    public static readonly string[] Architecture =
    [
        "Basit tek proje", "Katmanlı (N-tier)", "Clean Architecture", "MVVM (masaüstü)", "Vertical Slice"
    ];

    public static readonly string[] Features =
    [
        "Listeleme/filtreleme", "CRUD ekranları", "Excel import/export", "PDF export",
        "E-posta gönderimi", "Zamanlanmış görev", "Dosya yükleme", "Arama",
        "Log/audit trail", "Bildirim", "3. parti API entegrasyonu"
    ];

    public static readonly string[] UiStyle =
    [
        "Minimal", "Modern (Bootstrap/MudBlazor/MaterialDesign)", "Kurumsal/tablo ağırlıklı", "Dashboard/grafikli"
    ];

    public static readonly string[] DotnetVersion =
    [
        ".NET 8", ".NET 9", "Framework 4.8 (legacy)", "Farketmez"
    ];

    public static readonly string[] TestExpectation =
    [
        "Yok", "Unit test (xUnit/NUnit)", "Unit + Integration"
    ];

    public static readonly string[] Deployment =
    [
        "Local exe", "IIS", "Docker", "Azure", "Windows Service"
    ];

    public static readonly string[] BackendArchitecture =
    [
        "Monolit (arayüzle tek proje)", "Ayrı REST API + ayrı frontend",
        "Ayrı GraphQL API + frontend", "Sadece API (frontend yok)"
    ];

    public static readonly string[] ApiDocs =
    [
        "Swagger/OpenAPI ekle", "Gerek yok"
    ];

    public static readonly string[] Logging =
    [
        "Yok", "Built-in ILogger", "Serilog"
    ];

    public static readonly string[] ExtraLibraries =
    [
        "AutoMapper", "MediatR", "FluentValidation", "Yok/farketmez"
    ];

    public static readonly string[] Languages =
    [
        "C#", "SQL", "JavaScript/TypeScript", "PowerShell", "Python"
    ];

    public static readonly string[] ScriptInterpreter =
    [
        "Yok", "PowerShell", "Python", "Roslyn C# Scripting (CSX)"
    ];
}
