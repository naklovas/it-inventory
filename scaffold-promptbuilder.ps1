<#
.SYNOPSIS
    PromptBuilder projesini (dosyalari) sifirdan olusturur.
.DESCRIPTION
    Bu script, repo klonlamadan, gerekli tum kaynak dosyalarini
    (csproj, appsettings.json, *.razor, *.cs, wwwroot) calistigi dizinin
    altina yazar. Ardindan "dotnet build" ile derlemeyi dener.
.EXAMPLE
    .\scaffold-promptbuilder.ps1
#>

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$projectDir = Join-Path $root 'src/PromptBuilder'

New-Item -ItemType Directory -Force -Path $projectDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Components') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Components/Layout') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Components/Pages') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Components/Shared') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Models') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Properties') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'Services') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectDir 'wwwroot') | Out-Null

function Write-ProjectFile {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Content
    )
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
    Write-Host "  yazildi: $Path"
}

Write-Host "Dosyalar olusturuluyor..."

Write-ProjectFile -Path (Join-Path $projectDir 'PromptBuilder.csproj') -Content @'
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>PromptBuilder</RootNamespace>
  </PropertyGroup>

</Project>
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Program.cs') -Content @'
using PromptBuilder.Components;
using PromptBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<PromptGeneratorService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
'@

Write-ProjectFile -Path (Join-Path $projectDir 'appsettings.json') -Content @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Properties/launchSettings.json') -Content @'
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5140",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/_Imports.razor') -Content @'
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using PromptBuilder.Components
@using PromptBuilder.Components.Layout
@using PromptBuilder.Components.Shared
@using PromptBuilder.Models
@using PromptBuilder.Services
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/App.razor') -Content @'
<!DOCTYPE html>
<html lang="tr">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="app.css" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>

<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>

</html>
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/Routes.razor') -Content @'
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
    </Found>
    <NotFound>
        <LayoutView Layout="typeof(MainLayout)">
            <p>Sayfa bulunamadı.</p>
        </LayoutView>
    </NotFound>
</Router>
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/Layout/MainLayout.razor') -Content @'
@inherits LayoutComponentBase

<div class="page">
    <main>
        @Body
    </main>
</div>
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/Shared/SingleSelectField.razor') -Content @'
@namespace PromptBuilder.Components.Shared

<div class="field">
    <div class="field-label">@Label</div>
    <div class="options">
        @foreach (var opt in Options)
        {
            <label class="option">
                <input type="radio" name="@GroupName" checked="@(Value == opt)"
                       @onchange="@(() => OnSelect(opt))" />
                <span>@opt</span>
            </label>
        }
        @if (AllowOther)
        {
            <label class="option">
                <input type="radio" name="@GroupName" checked="@(Value == OtherOptionLabel)"
                       @onchange="@(() => OnSelect(OtherOptionLabel))" />
                <span>@OtherOptionLabel</span>
            </label>
        }
    </div>
    @if (AllowOther && Value == OtherOptionLabel)
    {
        <input class="other-input" placeholder="Belirtin..." value="@OtherText"
               @oninput="@(e => OtherTextChanged.InvokeAsync((string?)e.Value ?? ""))" />
    }
</div>

@code {
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter, EditorRequired] public string[] Options { get; set; } = [];
    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public bool AllowOther { get; set; } = true;
    [Parameter] public string OtherOptionLabel { get; set; } = "Diğer";
    [Parameter] public string OtherText { get; set; } = "";
    [Parameter] public EventCallback<string> OtherTextChanged { get; set; }

    private string GroupName => "grp-" + Label.GetHashCode();

    private Task OnSelect(string opt) => ValueChanged.InvokeAsync(opt);
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/Shared/MultiSelectField.razor') -Content @'
@namespace PromptBuilder.Components.Shared

<div class="field">
    <div class="field-label">@Label</div>
    <div class="options">
        @foreach (var opt in Options)
        {
            <label class="option">
                <input type="checkbox" checked="@Selected.Contains(opt)"
                       @onchange="@(e => Toggle(opt, (bool)(e.Value ?? false)))" />
                <span>@opt</span>
            </label>
        }
    </div>
    @if (AllowOther)
    {
        <input class="other-input" placeholder="Diğer (virgülle ayırın)..." value="@OtherText"
               @oninput="@(e => OtherTextChanged.InvokeAsync((string?)e.Value ?? ""))" />
    }
</div>

@code {
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter, EditorRequired] public string[] Options { get; set; } = [];
    [Parameter] public List<string> Selected { get; set; } = [];
    [Parameter] public EventCallback<List<string>> SelectedChanged { get; set; }
    [Parameter] public bool AllowOther { get; set; } = true;
    [Parameter] public string OtherText { get; set; } = "";
    [Parameter] public EventCallback<string> OtherTextChanged { get; set; }

    private Task Toggle(string opt, bool isChecked)
    {
        var updated = new List<string>(Selected);
        if (isChecked)
        {
            if (!updated.Contains(opt)) updated.Add(opt);
        }
        else
        {
            updated.Remove(opt);
        }
        return SelectedChanged.InvokeAsync(updated);
    }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/Pages/Wizard.razor') -Content @'
@page "/"
@inject PromptGeneratorService PromptGenerator
@inject IJSRuntime JS

<div class="wizard">
    <h1>C# Uygulama Prompt Builder</h1>
    <p class="intro">Alanları seçin, en altta hazır bir prompt oluşturulacak.</p>

    <div class="field">
        <div class="field-label">Proje adı</div>
        <input class="text-input" placeholder="Örn: StokTakip" @bind="_model.ProjectName" @bind:event="oninput" />
    </div>

    <SingleSelectField Label="Uygulama tipi" Options="WizardOptions.AppType"
                        @bind-Value="_model.AppType" @bind-OtherText="_model.AppTypeOther" />

    <SingleSelectField Label="Amaç/domain" Options="WizardOptions.Domain"
                        @bind-Value="_model.Domain" @bind-OtherText="_model.DomainOther" />

    <SingleSelectField Label="Ölçek" Options="WizardOptions.Scale" AllowOther="false"
                        @bind-Value="_model.Scale" />

    <SingleSelectField Label="Veri katmanı" Options="WizardOptions.DataLayer" AllowOther="false"
                        @bind-Value="_model.DataLayer" />

    @if (_model.DataLayer != "Yok (bellek içi)")
    {
        <SingleSelectField Label="Veri erişim yöntemi" Options="WizardOptions.AccessMethod" AllowOther="false"
                            @bind-Value="_model.AccessMethod" />
    }

    <SingleSelectField Label="Kimlik doğrulama" Options="WizardOptions.Auth" AllowOther="false"
                        @bind-Value="_model.Auth" />

    <SingleSelectField Label="Mimari" Options="WizardOptions.Architecture" AllowOther="false"
                        @bind-Value="_model.Architecture" />

    <SingleSelectField Label="Backend mimarisi" Options="WizardOptions.BackendArchitecture" AllowOther="false"
                        @bind-Value="_model.BackendArchitecture" />

    @if (_model.BackendArchitecture != "Monolit (arayüzle tek proje)" && !string.IsNullOrEmpty(_model.BackendArchitecture))
    {
        <SingleSelectField Label="API dokümantasyonu" Options="WizardOptions.ApiDocs" AllowOther="false"
                            @bind-Value="_model.ApiDocs" />
    }

    <MultiSelectField Label="Temel özellikler" Options="WizardOptions.Features"
                       @bind-Selected="_model.Features" @bind-OtherText="_model.FeaturesOther" />

    <SingleSelectField Label="UI stili" Options="WizardOptions.UiStyle" AllowOther="false"
                        @bind-Value="_model.UiStyle" />

    <SingleSelectField Label=".NET sürümü" Options="WizardOptions.DotnetVersion" AllowOther="false"
                        @bind-Value="_model.DotnetVersion" />

    <SingleSelectField Label="Loglama" Options="WizardOptions.Logging" AllowOther="false"
                        @bind-Value="_model.Logging" />

    <SingleSelectField Label="Test beklentisi" Options="WizardOptions.TestExpectation" AllowOther="false"
                        @bind-Value="_model.TestExpectation" />

    <SingleSelectField Label="Deployment" Options="WizardOptions.Deployment" AllowOther="false"
                        @bind-Value="_model.Deployment" />

    <MultiSelectField Label="Ek kütüphaneler" Options="WizardOptions.ExtraLibraries"
                       @bind-Selected="_model.ExtraLibraries" @bind-OtherText="_model.ExtraLibrariesOther" />

    <MultiSelectField Label="Kullanılacak diller" Options="WizardOptions.Languages"
                       @bind-Selected="_model.Languages" @bind-OtherText="_model.LanguagesOther" />

    <SingleSelectField Label="Script/otomasyon interpreter'ı" Options="WizardOptions.ScriptInterpreter" AllowOther="false"
                        @bind-Value="_model.ScriptInterpreter" />

    <div class="field">
        <div class="field-label">Ek notlar (opsiyonel)</div>
        <textarea class="text-area" rows="3" placeholder="Yukarıdaki alanlara sığmayan özel istekler..."
                  @bind="_model.ExtraNotes" @bind:event="oninput"></textarea>
    </div>

    <button class="generate-btn" @onclick="GeneratePrompt">Prompt Oluştur</button>

    @if (!string.IsNullOrEmpty(_generatedPrompt))
    {
        <div class="output">
            <div class="output-header">
                <span>Oluşan Prompt</span>
                <button class="copy-btn" @onclick="CopyToClipboard">Kopyala</button>
            </div>
            <textarea class="output-area" rows="16" readonly>@_generatedPrompt</textarea>
        </div>
    }
</div>

@code {
    private readonly WizardModel _model = new();
    private string _generatedPrompt = "";

    private void GeneratePrompt()
    {
        _generatedPrompt = PromptGenerator.Generate(_model);
    }

    private Task CopyToClipboard() =>
        JS.InvokeVoidAsync("navigator.clipboard.writeText", _generatedPrompt).AsTask();
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardOptions.cs') -Content @'
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
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardModel.cs') -Content @'
namespace PromptBuilder.Models;

public class WizardModel
{
    public string ProjectName { get; set; } = "";

    public string AppType { get; set; } = "";
    public string AppTypeOther { get; set; } = "";

    public string Domain { get; set; } = "";
    public string DomainOther { get; set; } = "";

    public string Scale { get; set; } = "";

    public string DataLayer { get; set; } = "";

    public string AccessMethod { get; set; } = "";

    public string Auth { get; set; } = "";

    public string Architecture { get; set; } = "";

    public List<string> Features { get; set; } = [];
    public string FeaturesOther { get; set; } = "";

    public string UiStyle { get; set; } = "";

    public string DotnetVersion { get; set; } = "";

    public string TestExpectation { get; set; } = "";

    public string Deployment { get; set; } = "";

    public string BackendArchitecture { get; set; } = "";

    public string ApiDocs { get; set; } = "";

    public string Logging { get; set; } = "";

    public List<string> ExtraLibraries { get; set; } = [];
    public string ExtraLibrariesOther { get; set; } = "";

    public List<string> Languages { get; set; } = [];
    public string LanguagesOther { get; set; } = "";

    public string ScriptInterpreter { get; set; } = "";

    public string ExtraNotes { get; set; } = "";
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Services/PromptGeneratorService.cs') -Content @'
using System.Text;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class PromptGeneratorService
{
    public string Generate(WizardModel m)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:");
        sb.AppendLine();

        AppendLine(sb, "Proje adı", m.ProjectName);
        AppendLine(sb, "Uygulama tipi", Resolve(m.AppType, m.AppTypeOther));
        AppendLine(sb, "Amaç/domain", Resolve(m.Domain, m.DomainOther));
        AppendLine(sb, "Ölçek", m.Scale);
        AppendLine(sb, "Veri katmanı", m.DataLayer);
        if (m.DataLayer != "Yok (bellek içi)")
        {
            AppendLine(sb, "Veri erişim yöntemi", m.AccessMethod);
        }
        AppendLine(sb, "Kimlik doğrulama", m.Auth);
        AppendLine(sb, "Mimari", m.Architecture);
        AppendLine(sb, "Backend mimarisi", m.BackendArchitecture);
        if (m.BackendArchitecture != "Monolit (arayüzle tek proje)")
        {
            AppendLine(sb, "API dokümantasyonu", m.ApiDocs);
        }
        AppendLine(sb, "Temel özellikler", Resolve(m.Features, m.FeaturesOther));
        AppendLine(sb, "UI stili", m.UiStyle);
        AppendLine(sb, ".NET sürümü", m.DotnetVersion);
        AppendLine(sb, "Loglama", m.Logging);
        AppendLine(sb, "Test beklentisi", m.TestExpectation);
        AppendLine(sb, "Deployment", m.Deployment);
        AppendLine(sb, "Ek kütüphaneler", Resolve(m.ExtraLibraries, m.ExtraLibrariesOther));
        AppendLine(sb, "Kullanılacak diller", Resolve(m.Languages, m.LanguagesOther));
        if (m.ScriptInterpreter != "Yok")
        {
            AppendLine(sb, "Script/otomasyon interpreter'ı", m.ScriptInterpreter);
        }

        if (!string.IsNullOrWhiteSpace(m.ExtraNotes))
        {
            sb.AppendLine();
            sb.AppendLine("Ek notlar:");
            sb.AppendLine(m.ExtraNotes.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.");

        return sb.ToString();
    }

    private static string Resolve(string value, string other) =>
        value == "Diğer" && !string.IsNullOrWhiteSpace(other) ? other : value;

    private static string Resolve(List<string> values, string other)
    {
        var items = new List<string>(values);
        if (!string.IsNullOrWhiteSpace(other))
        {
            items.AddRange(other.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        return items.Count > 0 ? string.Join(", ", items) : "";
    }

    private static void AppendLine(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($"- {label}: {value}");
    }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'wwwroot/app.css') -Content @'
* {
    box-sizing: border-box;
}

body {
    font-family: Segoe UI, Arial, sans-serif;
    background: #f4f5f7;
    margin: 0;
    color: #1f2430;
}

.wizard {
    max-width: 780px;
    margin: 0 auto;
    padding: 32px 20px 64px;
}

h1 {
    font-size: 1.6rem;
    margin-bottom: 4px;
}

.intro {
    color: #5b6270;
    margin-top: 0;
    margin-bottom: 24px;
}

.field {
    background: #fff;
    border: 1px solid #e2e4e9;
    border-radius: 8px;
    padding: 14px 16px;
    margin-bottom: 12px;
}

.field-label {
    font-weight: 600;
    margin-bottom: 8px;
}

.options {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 16px;
}

.option {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 0.95rem;
    cursor: pointer;
}

.other-input,
.text-input,
.text-area,
.output-area {
    width: 100%;
    margin-top: 10px;
    padding: 8px 10px;
    border: 1px solid #d3d6dd;
    border-radius: 6px;
    font-family: inherit;
    font-size: 0.95rem;
}

.text-input {
    margin-top: 0;
}

.generate-btn,
.copy-btn {
    background: #2f6fed;
    color: #fff;
    border: none;
    border-radius: 6px;
    padding: 10px 18px;
    font-size: 0.95rem;
    cursor: pointer;
}

.generate-btn {
    display: block;
    margin: 20px 0;
}

.copy-btn {
    padding: 6px 12px;
    font-size: 0.85rem;
}

.output {
    background: #fff;
    border: 1px solid #e2e4e9;
    border-radius: 8px;
    padding: 14px 16px;
}

.output-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    font-weight: 600;
    margin-bottom: 8px;
}

.output-area {
    font-family: Consolas, monospace;
    resize: vertical;
}
'@

Write-Host ""
Write-Host "Tamamlandi. Simdi 'dotnet build' deneniyor..."

Push-Location $projectDir
try {
    dotnet build
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Calistirmak icin: dotnet run --project $projectDir"
Write-Host "(varsayilan adres: http://localhost:5140)"
