<#
.SYNOPSIS
    PromptBuilder projesini (dosyalari) sifirdan olusturur.
.DESCRIPTION
    Bu script, repo klonlamadan, gerekli tum kaynak dosyalarini
    (csproj, appsettings.json, *.razor, *.cs, wwwroot) calistigi dizinin
    altina, sql/promptbuilder_schema.sql'i de sql/ altina yazar. Ardindan
    "dotnet build" ile derlemeyi dener. Tum dosyalar UTF-8 BOM'lu yazilir -
    Turkce karakterlerin (sqlcmd/SSMS gibi araclarda) bozulmamasi icin.

    Sorular (WizardField/WizardOption) SQL Server'dan okunuyor - calistirmadan
    once appsettings.json > ConnectionStrings:PromptBuilderDb'yi doldurup
    sql/promptbuilder_schema.sql'i o veritabaninda calistirmaniz gerekir.
    Sayfada sag ustte TR/EN dil secici var; sorular ve olusan prompt metni
    secime gore Turkce ya da Ingilizce gosterilir.
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
    # UTF-8 WITH BOM: sqlcmd/SSMS ve bazi araclar BOM olmadan dosyayi sistem
    # codepage'i sanip Turkce karakterleri (ı, ğ, ş, ö, ü, ç, İ) bozabiliyor.
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8Bom)
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
        @foreach (var choice in Options)
        {
            <label class="option">
                <input type="radio" name="@GroupName" checked="@(Value == choice.Value)"
                       @onchange="@(() => OnSelect(choice.Value))" />
                <span>@choice.Display</span>
            </label>
        }
        @if (AllowOther)
        {
            <label class="option">
                <input type="radio" name="@GroupName" checked="@(Value == WizardConstants.OtherSentinel)"
                       @onchange="@(() => OnSelect(WizardConstants.OtherSentinel))" />
                <span>@OtherOptionLabel</span>
            </label>
        }
    </div>
    @if (AllowOther && Value == WizardConstants.OtherSentinel)
    {
        <input class="other-input" placeholder="@OtherPlaceholder" value="@OtherText"
               @oninput="@(e => OtherTextChanged.InvokeAsync((string?)e.Value ?? ""))" />
    }
</div>

@code {
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter, EditorRequired] public List<WizardChoice> Options { get; set; } = [];
    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public bool AllowOther { get; set; } = true;
    [Parameter] public string OtherOptionLabel { get; set; } = "Diğer";
    [Parameter] public string OtherPlaceholder { get; set; } = "Belirtin...";
    [Parameter] public string OtherText { get; set; } = "";
    [Parameter] public EventCallback<string> OtherTextChanged { get; set; }

    private string GroupName => "grp-" + Label.GetHashCode();

    private Task OnSelect(string value) => ValueChanged.InvokeAsync(value);
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Components/Shared/MultiSelectField.razor') -Content @'
@namespace PromptBuilder.Components.Shared

<div class="field">
    <div class="field-label">@Label</div>
    <div class="options">
        @foreach (var choice in Options)
        {
            <label class="option">
                <input type="checkbox" checked="@Selected.Contains(choice.Value)"
                       @onchange="@(e => Toggle(choice.Value, (bool)(e.Value ?? false)))" />
                <span>@choice.Display</span>
            </label>
        }
    </div>
    @if (AllowOther)
    {
        <input class="other-input" placeholder="@OtherPlaceholder" value="@OtherText"
               @oninput="@(e => OtherTextChanged.InvokeAsync((string?)e.Value ?? ""))" />
    }
</div>

@code {
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter, EditorRequired] public List<WizardChoice> Options { get; set; } = [];
    [Parameter] public List<string> Selected { get; set; } = [];
    [Parameter] public EventCallback<List<string>> SelectedChanged { get; set; }
    [Parameter] public bool AllowOther { get; set; } = true;
    [Parameter] public string OtherPlaceholder { get; set; } = "Diğer (virgülle ayırın)...";
    [Parameter] public string OtherText { get; set; } = "";
    [Parameter] public EventCallback<string> OtherTextChanged { get; set; }

    private Task Toggle(string value, bool isChecked)
    {
        var updated = new List<string>(Selected);
        if (isChecked)
        {
            if (!updated.Contains(value)) updated.Add(value);
        }
        else
        {
            updated.Remove(value);
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
    <div class="lang-toggle">
        <button class="@LangButtonClass(UiLanguage.Tr)" @onclick="@(() => SetLanguage(UiLanguage.Tr))">TR</button>
        <button class="@LangButtonClass(UiLanguage.En)" @onclick="@(() => SetLanguage(UiLanguage.En))">EN</button>
    </div>

    <h1>@_ui.PageTitle</h1>
    <p class="intro">@_ui.Intro</p>

    @if (_loadError is not null)
    {
        <div class="field error">@_loadError</div>
    }
    else if (_fields is null)
    {
        <p>@_ui.LoadingText</p>
    }
    else
    {
        <div class="field">
            <div class="field-label">@_ui.ProjectNameLabel</div>
            <input class="text-input" placeholder="@_ui.ProjectNamePlaceholder" @bind="_model.ProjectName" @bind:event="oninput" />
        </div>

        @foreach (var field in _fields)
        {
            if (IsHidden(field)) continue;

            var choices = field.Options.Select(o => new WizardChoice(o.Tr, o.For(_model.Language))).ToList();

            @if (field.FieldType == WizardFieldType.SingleSelect)
            {
                <SingleSelectField Label="@field.Label(_model.Language)" Options="choices" AllowOther="field.AllowOther"
                                    OtherOptionLabel="@_ui.OtherLabel" OtherPlaceholder="@_ui.OtherPlaceholder"
                                    Value="@GetSingle(field.FieldKey)"
                                    ValueChanged="@(v => SetSingle(field.FieldKey, v))"
                                    OtherText="@GetOther(field.FieldKey)"
                                    OtherTextChanged="@(v => SetOther(field.FieldKey, v))" />
            }
            else
            {
                <MultiSelectField Label="@field.Label(_model.Language)" Options="choices"
                                   Selected="@GetMulti(field.FieldKey)"
                                   SelectedChanged="@(v => SetMulti(field.FieldKey, v))"
                                   AllowOther="field.AllowOther" OtherPlaceholder="@_ui.OtherPlaceholderMulti"
                                   OtherText="@GetOther(field.FieldKey)"
                                   OtherTextChanged="@(v => SetOther(field.FieldKey, v))" />
            }
        }

        <div class="field">
            <div class="field-label">@_ui.ExtraNotesLabel</div>
            <textarea class="text-area" rows="3" placeholder="@_ui.ExtraNotesPlaceholder"
                      @bind="_model.ExtraNotes" @bind:event="oninput"></textarea>
        </div>

        <button class="generate-btn" @onclick="GeneratePrompt">@_ui.GenerateButton</button>

        @if (!string.IsNullOrEmpty(_generatedPrompt))
        {
            <div class="output">
                <div class="output-header">
                    <span>@_ui.OutputHeader</span>
                    <button class="copy-btn" @onclick="CopyToClipboard">@_ui.CopyButton</button>
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
    private UiStrings _ui = UiStrings.Tr;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _fields = await OptionsRepository.GetFieldsAsync();
        }
        catch (Exception ex)
        {
            _loadError = $"{UiStrings.Tr.LoadErrorPrefix} {ex.Message}";
        }
    }

    private void SetLanguage(UiLanguage lang)
    {
        _model.Language = lang;
        _ui = UiStrings.For(lang);
    }

    private string LangButtonClass(UiLanguage lang) =>
        _model.Language == lang ? "lang-btn active" : "lang-btn";

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

Write-ProjectFile -Path (Join-Path $projectDir 'Models/UiLanguage.cs') -Content @'
namespace PromptBuilder.Models;

public enum UiLanguage
{
    Tr,
    En
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/UiStrings.cs') -Content @'
namespace PromptBuilder.Models;

public class UiStrings
{
    public string PageTitle { get; init; } = "";
    public string Intro { get; init; } = "";
    public string ProjectNameLabel { get; init; } = "";
    public string ProjectNamePlaceholder { get; init; } = "";
    public string ExtraNotesLabel { get; init; } = "";
    public string ExtraNotesPlaceholder { get; init; } = "";
    public string GenerateButton { get; init; } = "";
    public string LoadingText { get; init; } = "";
    public string LoadErrorPrefix { get; init; } = "";
    public string OutputHeader { get; init; } = "";
    public string CopyButton { get; init; } = "";
    public string OtherLabel { get; init; } = "";
    public string OtherPlaceholder { get; init; } = "";
    public string OtherPlaceholderMulti { get; init; } = "";

    public string PromptIntro { get; init; } = "";
    public string ExtraNotesHeading { get; init; } = "";
    public string PromptOutro { get; init; } = "";

    public static readonly UiStrings Tr = new()
    {
        PageTitle = "C# Uygulama Prompt Builder",
        Intro = "Alanları seçin, en altta hazır bir prompt oluşturulacak. Sorular SQL Server'daki " +
                "dbo.WizardField / dbo.WizardOption tablolarından geliyor.",
        ProjectNameLabel = "Proje adı",
        ProjectNamePlaceholder = "Örn: StokTakip",
        ExtraNotesLabel = "Ek notlar (opsiyonel)",
        ExtraNotesPlaceholder = "Yukarıdaki alanlara sığmayan özel istekler...",
        GenerateButton = "Prompt Oluştur",
        LoadingText = "Yükleniyor...",
        LoadErrorPrefix = "Alanlar veritabanından yüklenemedi:",
        OutputHeader = "Oluşan Prompt",
        CopyButton = "Kopyala",
        OtherLabel = "Diğer",
        OtherPlaceholder = "Belirtin...",
        OtherPlaceholderMulti = "Diğer (virgülle ayırın)...",
        PromptIntro = "Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:",
        ExtraNotesHeading = "Ek notlar:",
        PromptOutro = "Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.",
    };

    public static readonly UiStrings En = new()
    {
        PageTitle = "C# App Prompt Builder",
        Intro = "Pick the fields below; a ready-to-use prompt will be generated at the bottom. Questions " +
                "come from the dbo.WizardField / dbo.WizardOption tables in SQL Server.",
        ProjectNameLabel = "Project name",
        ProjectNamePlaceholder = "e.g. StockTracker",
        ExtraNotesLabel = "Additional notes (optional)",
        ExtraNotesPlaceholder = "Any special requests not covered above...",
        GenerateButton = "Generate Prompt",
        LoadingText = "Loading...",
        LoadErrorPrefix = "Failed to load fields from the database:",
        OutputHeader = "Generated Prompt",
        CopyButton = "Copy",
        OtherLabel = "Other",
        OtherPlaceholder = "Please specify...",
        OtherPlaceholderMulti = "Other (comma-separated)...",
        PromptIntro = "I want you to build a C# application that meets the following requirements:",
        ExtraNotesHeading = "Additional notes:",
        PromptOutro = "Please produce a well-structured, best-practice C# project skeleton that meets " +
                       "these requirements and compiles. State any assumptions you make.",
    };

    public static UiStrings For(UiLanguage lang) => lang == UiLanguage.En ? En : Tr;
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardChoice.cs') -Content @'
namespace PromptBuilder.Models;

public record WizardChoice(string Value, string Display);
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardConstants.cs') -Content @'
namespace PromptBuilder.Models;

public static class WizardConstants
{
    public const string OtherSentinel = "__wizard_other__";
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
    public string LabelTr { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public WizardFieldType FieldType { get; set; }
    public bool AllowOther { get; set; }
    public int SortOrder { get; set; }
    public string? ConditionalOnFieldKey { get; set; }
    public string? ConditionalHiddenValue { get; set; }
    public List<WizardOptionText> Options { get; set; } = [];

    public string Label(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(LabelEn) ? LabelEn : LabelTr;
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardModel.cs') -Content @'
namespace PromptBuilder.Models;

public class WizardModel
{
    public UiLanguage Language { get; set; } = UiLanguage.Tr;

    public string ProjectName { get; set; } = "";

    public Dictionary<string, string> SingleValues { get; set; } = new();
    public Dictionary<string, List<string>> MultiValues { get; set; } = new();
    public Dictionary<string, string> OtherValues { get; set; } = new();

    public string ExtraNotes { get; set; } = "";
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardOptionText.cs') -Content @'
namespace PromptBuilder.Models;

public class WizardOptionText
{
    public string Tr { get; set; } = "";
    public string En { get; set; } = "";

    public string For(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(En) ? En : Tr;
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
            SELECT FieldId, FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder,
                   ConditionalOnFieldKey, ConditionalHiddenValue
            FROM dbo.WizardField
            ORDER BY SortOrder;
            """;

        await using (var command = new SqlCommand(fieldSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var labelTr = reader.GetString(2);
                var definition = new WizardFieldDefinition
                {
                    FieldKey = reader.GetString(1),
                    LabelTr = labelTr,
                    LabelEn = reader.IsDBNull(3) ? labelTr : reader.GetString(3),
                    FieldType = Enum.Parse<WizardFieldType>(reader.GetString(4)),
                    AllowOther = reader.GetBoolean(5),
                    SortOrder = reader.GetInt32(6),
                    ConditionalOnFieldKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ConditionalHiddenValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                };
                fields.Add((reader.GetInt32(0), definition));
            }
        }

        const string optionSql = """
            SELECT OptionText, OptionTextEn
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
                var tr = reader.GetString(0);
                definition.Options.Add(new WizardOptionText
                {
                    Tr = tr,
                    En = reader.IsDBNull(1) ? tr : reader.GetString(1),
                });
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
        var ui = UiStrings.For(model.Language);
        var sb = new StringBuilder();

        sb.AppendLine(ui.PromptIntro);
        sb.AppendLine();

        AppendLine(sb, ui.ProjectNameLabel, model.ProjectName);

        foreach (var field in fields)
        {
            if (IsHidden(field, model)) continue;

            var value = field.FieldType == WizardFieldType.MultiSelect
                ? ResolveMulti(model, field)
                : ResolveSingle(model, field);

            AppendLine(sb, field.Label(model.Language), value);
        }

        if (!string.IsNullOrWhiteSpace(model.ExtraNotes))
        {
            sb.AppendLine();
            sb.AppendLine(ui.ExtraNotesHeading);
            sb.AppendLine(model.ExtraNotes.Trim());
        }

        sb.AppendLine();
        sb.AppendLine(ui.PromptOutro);

        return sb.ToString();
    }

    private static bool IsHidden(WizardFieldDefinition field, WizardModel model)
    {
        if (field.ConditionalOnFieldKey is null) return false;
        var parentValue = model.SingleValues.GetValueOrDefault(field.ConditionalOnFieldKey, "");
        return parentValue == field.ConditionalHiddenValue;
    }

    private static string ResolveSingle(WizardModel model, WizardFieldDefinition field)
    {
        var value = model.SingleValues.GetValueOrDefault(field.FieldKey, "");
        if (value == WizardConstants.OtherSentinel)
        {
            return model.OtherValues.GetValueOrDefault(field.FieldKey, "");
        }

        var option = field.Options.FirstOrDefault(o => o.Tr == value);
        return option?.For(model.Language) ?? "";
    }

    private static string ResolveMulti(WizardModel model, WizardFieldDefinition field)
    {
        var selected = model.MultiValues.GetValueOrDefault(field.FieldKey, []);
        var items = new List<string>();
        foreach (var value in selected)
        {
            var option = field.Options.FirstOrDefault(o => o.Tr == value);
            if (option is not null) items.Add(option.For(model.Language));
        }

        var other = model.OtherValues.GetValueOrDefault(field.FieldKey, "");
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

.lang-toggle {
    display: flex;
    justify-content: flex-end;
    gap: 6px;
    margin-bottom: 12px;
}

.lang-btn {
    background: #fff;
    border: 1px solid #d3d6dd;
    border-radius: 6px;
    padding: 4px 12px;
    font-size: 0.85rem;
    cursor: pointer;
}

.lang-btn.active {
    background: #2f6fed;
    border-color: #2f6fed;
    color: #fff;
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
