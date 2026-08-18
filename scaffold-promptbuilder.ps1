<#
.SYNOPSIS
    PromptBuilder projesini (dosyalari) sifirdan olusturur.
.DESCRIPTION
    Bu script, repo klonlamadan, gerekli tum kaynak dosyalarini
    (csproj, appsettings.json, *.razor, *.cs, wwwroot) calistigi dizinin
    altina, sql/promptbuilder_schema.sql'i de sql/ altina yazar. Ardindan
    "dotnet build" ile derlemeyi dener.

    Sorular (WizardField/WizardOption) SQL Server'dan okunuyor - calistirmadan
    once appsettings.json > ConnectionStrings:PromptBuilderDb'yi doldurup
    sql/promptbuilder_schema.sql'i o veritabaninda calistirmaniz gerekir.
.EXAMPLE
    .\scaffold-promptbuilder.ps1
#>

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$projectDir = Join-Path $root 'src/PromptBuilder'
$sqlDir = Join-Path $root 'sql'

New-Item -ItemType Directory -Force -Path $projectDir | Out-Null
New-Item -ItemType Directory -Force -Path $sqlDir | Out-Null
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

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>

</Project>
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Program.cs') -Content @'
using PromptBuilder.Components;
using PromptBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<PromptGeneratorService>();
builder.Services.AddScoped<WizardOptionsRepository>();

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
  "ConnectionStrings": {
    "PromptBuilderDb": ""
  },
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
@using Microsoft.JSInterop
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
@inject WizardOptionsRepository OptionsRepository
@inject PromptGeneratorService PromptGenerator
@inject IJSRuntime JS

<div class="wizard">
    <h1>C# Uygulama Prompt Builder</h1>
    <p class="intro">Alanları seçin, en altta hazır bir prompt oluşturulacak. Sorular SQL Server'daki
        dbo.WizardField / dbo.WizardOption tablolarından geliyor.</p>

    @if (_loadError is not null)
    {
        <div class="field error">@_loadError</div>
    }
    else if (_fields is null)
    {
        <p>Yükleniyor...</p>
    }
    else
    {
        <div class="field">
            <div class="field-label">Proje adı</div>
            <input class="text-input" placeholder="Örn: StokTakip" @bind="_model.ProjectName" @bind:event="oninput" />
        </div>

        @foreach (var field in _fields)
        {
            if (IsHidden(field)) continue;

            @if (field.FieldType == WizardFieldType.SingleSelect)
            {
                <SingleSelectField Label="@field.Label" Options="field.Options.ToArray()" AllowOther="field.AllowOther"
                                    Value="@GetSingle(field.FieldKey)"
                                    ValueChanged="@(v => SetSingle(field.FieldKey, v))"
                                    OtherText="@GetOther(field.FieldKey)"
                                    OtherTextChanged="@(v => SetOther(field.FieldKey, v))" />
            }
            else
            {
                <MultiSelectField Label="@field.Label" Options="field.Options.ToArray()"
                                   Selected="@GetMulti(field.FieldKey)"
                                   SelectedChanged="@(v => SetMulti(field.FieldKey, v))"
                                   AllowOther="field.AllowOther"
                                   OtherText="@GetOther(field.FieldKey)"
                                   OtherTextChanged="@(v => SetOther(field.FieldKey, v))" />
            }
        }

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
    }
</div>

@code {
    private readonly WizardModel _model = new();
    private List<WizardFieldDefinition>? _fields;
    private string? _loadError;
    private string _generatedPrompt = "";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _fields = await OptionsRepository.GetFieldsAsync();
        }
        catch (Exception ex)
        {
            _loadError = $"Alanlar veritabanından yüklenemedi: {ex.Message}";
        }
    }

    private bool IsHidden(WizardFieldDefinition field)
    {
        if (field.ConditionalOnFieldKey is null) return false;
        return GetSingle(field.ConditionalOnFieldKey) == field.ConditionalHiddenValue;
    }

    private string GetSingle(string key) => _model.SingleValues.GetValueOrDefault(key, "");
    private void SetSingle(string key, string value) => _model.SingleValues[key] = value;

    private List<string> GetMulti(string key) => _model.MultiValues.GetValueOrDefault(key, []);
    private void SetMulti(string key, List<string> value) => _model.MultiValues[key] = value;

    private string GetOther(string key) => _model.OtherValues.GetValueOrDefault(key, "");
    private void SetOther(string key, string value) => _model.OtherValues[key] = value;

    private void GeneratePrompt()
    {
        _generatedPrompt = PromptGenerator.Generate(_model, _fields ?? []);
    }

    private Task CopyToClipboard() =>
        JS.InvokeVoidAsync("navigator.clipboard.writeText", _generatedPrompt).AsTask();
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardFieldDefinition.cs') -Content @'
namespace PromptBuilder.Models;

public enum WizardFieldType
{
    SingleSelect,
    MultiSelect
}

public class WizardFieldDefinition
{
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public WizardFieldType FieldType { get; set; }
    public bool AllowOther { get; set; }
    public int SortOrder { get; set; }
    public string? ConditionalOnFieldKey { get; set; }
    public string? ConditionalHiddenValue { get; set; }
    public List<string> Options { get; set; } = [];
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardModel.cs') -Content @'
namespace PromptBuilder.Models;

public class WizardModel
{
    public string ProjectName { get; set; } = "";

    public Dictionary<string, string> SingleValues { get; set; } = new();
    public Dictionary<string, List<string>> MultiValues { get; set; } = new();
    public Dictionary<string, string> OtherValues { get; set; } = new();

    public string ExtraNotes { get; set; } = "";
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Services/WizardOptionsRepository.cs') -Content @'
using Microsoft.Data.SqlClient;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class WizardOptionsRepository
{
    private readonly string _connectionString;

    public WizardOptionsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PromptBuilderDb")
            ?? throw new InvalidOperationException(
                "appsettings.json: ConnectionStrings:PromptBuilderDb bos birakilamaz.");
    }

    public async Task<List<WizardFieldDefinition>> GetFieldsAsync(CancellationToken ct = default)
    {
        var fields = new List<(int FieldId, WizardFieldDefinition Definition)>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string fieldSql = """
            SELECT FieldId, FieldKey, Label, FieldType, AllowOther, SortOrder,
                   ConditionalOnFieldKey, ConditionalHiddenValue
            FROM dbo.WizardField
            ORDER BY SortOrder;
            """;

        await using (var command = new SqlCommand(fieldSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var definition = new WizardFieldDefinition
                {
                    FieldKey = reader.GetString(1),
                    Label = reader.GetString(2),
                    FieldType = Enum.Parse<WizardFieldType>(reader.GetString(3)),
                    AllowOther = reader.GetBoolean(4),
                    SortOrder = reader.GetInt32(5),
                    ConditionalOnFieldKey = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ConditionalHiddenValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                };
                fields.Add((reader.GetInt32(0), definition));
            }
        }

        const string optionSql = """
            SELECT OptionText
            FROM dbo.WizardOption
            WHERE FieldId = @FieldId
            ORDER BY SortOrder;
            """;

        foreach (var (fieldId, definition) in fields)
        {
            await using var command = new SqlCommand(optionSql, connection);
            command.Parameters.AddWithValue("@FieldId", fieldId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                definition.Options.Add(reader.GetString(0));
            }
        }

        return fields.Select(f => f.Definition).ToList();
    }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Services/PromptGeneratorService.cs') -Content @'
using System.Text;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class PromptGeneratorService
{
    public string Generate(WizardModel model, List<WizardFieldDefinition> fields)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:");
        sb.AppendLine();

        AppendLine(sb, "Proje adı", model.ProjectName);

        foreach (var field in fields)
        {
            if (IsHidden(field, model)) continue;

            var value = field.FieldType == WizardFieldType.MultiSelect
                ? ResolveMulti(model, field.FieldKey)
                : ResolveSingle(model, field.FieldKey);

            AppendLine(sb, field.Label, value);
        }

        if (!string.IsNullOrWhiteSpace(model.ExtraNotes))
        {
            sb.AppendLine();
            sb.AppendLine("Ek notlar:");
            sb.AppendLine(model.ExtraNotes.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.");

        return sb.ToString();
    }

    private static bool IsHidden(WizardFieldDefinition field, WizardModel model)
    {
        if (field.ConditionalOnFieldKey is null) return false;
        var parentValue = model.SingleValues.GetValueOrDefault(field.ConditionalOnFieldKey, "");
        return parentValue == field.ConditionalHiddenValue;
    }

    private static string ResolveSingle(WizardModel model, string fieldKey)
    {
        var value = model.SingleValues.GetValueOrDefault(fieldKey, "");
        var other = model.OtherValues.GetValueOrDefault(fieldKey, "");
        return value == "Diğer" && !string.IsNullOrWhiteSpace(other) ? other : value;
    }

    private static string ResolveMulti(WizardModel model, string fieldKey)
    {
        var items = new List<string>(model.MultiValues.GetValueOrDefault(fieldKey, []));
        var other = model.OtherValues.GetValueOrDefault(fieldKey, "");
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

.field.error {
    border-color: #d64545;
    color: #b3261e;
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

Write-ProjectFile -Path (Join-Path $sqlDir 'promptbuilder_schema.sql') -Content @'
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
Write-Host "Kalan adimlar:"
Write-Host "  1) appsettings.json > ConnectionStrings:PromptBuilderDb'yi doldurun."
Write-Host "  2) sql/promptbuilder_schema.sql'i o SQL Server veritabaninda calistirin"
Write-Host "     (tablolari olusturur ve sorulari/secenekleri bir kere doldurur)."
Write-Host "  3) dotnet run --project $projectDir  (varsayilan adres: http://localhost:5140)"
