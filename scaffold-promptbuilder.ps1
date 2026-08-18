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
    secime gore Turkce ya da Ingilizce gosterilir. Her alanin yaninda (i)
    ikonuyla acilan bir yardim metni, her secenekte de hover'da tooltip
    olarak "ne ise yarar / ne zaman tercih edilmeli" aciklamasi var. Temel
    ozellikler alaninda secilen her ozellige ozel bir not da eklenebilir.
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
    <div class="field-label-row">
        <div class="field-label">@Label</div>
        @if (!string.IsNullOrWhiteSpace(FieldHelp))
        {
            <details class="field-help">
                <summary>ⓘ</summary>
                <p>@FieldHelp</p>
            </details>
        }
    </div>
    <div class="options">
        @foreach (var choice in Options)
        {
            <label class="option" title="@choice.Help">
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
    [Parameter] public string FieldHelp { get; set; } = "";
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
    <div class="field-label-row">
        <div class="field-label">@Label</div>
        @if (!string.IsNullOrWhiteSpace(FieldHelp))
        {
            <details class="field-help">
                <summary>ⓘ</summary>
                <p>@FieldHelp</p>
            </details>
        }
    </div>
    <div class="options">
        @foreach (var choice in Options)
        {
            <div class="option-row">
                <label class="option" title="@choice.Help">
                    <input type="checkbox" checked="@Selected.Contains(choice.Value)"
                           @onchange="@(e => Toggle(choice.Value, (bool)(e.Value ?? false)))" />
                    <span>@choice.Display</span>
                </label>
                @if (AllowItemNotes && Selected.Contains(choice.Value))
                {
                    <input class="item-note-input" placeholder="@ItemNotePlaceholder"
                           value="@GetItemNote(choice.Value)"
                           @oninput="@(e => OnItemNoteInput(choice.Value, (string?)e.Value ?? ""))" />
                }
            </div>
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
    [Parameter] public string FieldHelp { get; set; } = "";
    [Parameter, EditorRequired] public List<WizardChoice> Options { get; set; } = [];
    [Parameter] public List<string> Selected { get; set; } = [];
    [Parameter] public EventCallback<List<string>> SelectedChanged { get; set; }
    [Parameter] public bool AllowOther { get; set; } = true;
    [Parameter] public string OtherPlaceholder { get; set; } = "Diğer (virgülle ayırın)...";
    [Parameter] public string OtherText { get; set; } = "";
    [Parameter] public EventCallback<string> OtherTextChanged { get; set; }
    [Parameter] public bool AllowItemNotes { get; set; }
    [Parameter] public Dictionary<string, string> ItemNotes { get; set; } = new();
    [Parameter] public EventCallback<(string Value, string Note)> ItemNoteChanged { get; set; }
    [Parameter] public string ItemNotePlaceholder { get; set; } = "Not ekleyin (opsiyonel)...";

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

    private string GetItemNote(string value) => ItemNotes.GetValueOrDefault(value, "");

    private Task OnItemNoteInput(string value, string note) => ItemNoteChanged.InvokeAsync((value, note));
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

            var choices = field.Options
                .Select(o => new WizardChoice(o.Tr, o.For(_model.Language), o.HelpFor(_model.Language)))
                .ToList();

            @if (field.FieldType == WizardFieldType.SingleSelect)
            {
                <SingleSelectField Label="@field.Label(_model.Language)" FieldHelp="@field.Help(_model.Language)"
                                    Options="choices" AllowOther="field.AllowOther"
                                    OtherOptionLabel="@_ui.OtherLabel" OtherPlaceholder="@_ui.OtherPlaceholder"
                                    Value="@GetSingle(field.FieldKey)"
                                    ValueChanged="@(v => SetSingle(field.FieldKey, v))"
                                    OtherText="@GetOther(field.FieldKey)"
                                    OtherTextChanged="@(v => SetOther(field.FieldKey, v))" />
            }
            else
            {
                <MultiSelectField Label="@field.Label(_model.Language)" FieldHelp="@field.Help(_model.Language)"
                                   Options="choices"
                                   Selected="@GetMulti(field.FieldKey)"
                                   SelectedChanged="@(v => SetMulti(field.FieldKey, v))"
                                   AllowOther="field.AllowOther" OtherPlaceholder="@_ui.OtherPlaceholderMulti"
                                   OtherText="@GetOther(field.FieldKey)"
                                   OtherTextChanged="@(v => SetOther(field.FieldKey, v))"
                                   AllowItemNotes="field.AllowItemNotes" ItemNotePlaceholder="@_ui.ItemNotePlaceholder"
                                   ItemNotes="@GetItemNotes(field.FieldKey)"
                                   ItemNoteChanged="@(args => SetItemNote(field.FieldKey, args.Value, args.Note))" />
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

    private Dictionary<string, string> GetItemNotes(string key) =>
        _model.ItemNotes.TryGetValue(key, out var notes) ? notes : new Dictionary<string, string>();

    private void SetItemNote(string fieldKey, string value, string note)
    {
        if (!_model.ItemNotes.TryGetValue(fieldKey, out var notes))
        {
            notes = new Dictionary<string, string>();
            _model.ItemNotes[fieldKey] = notes;
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            notes.Remove(value);
        }
        else
        {
            notes[value] = note;
        }
    }

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
    public string ItemNotePlaceholder { get; init; } = "";

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
        ItemNotePlaceholder = "Not ekleyin (opsiyonel)...",
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
        ItemNotePlaceholder = "Add a note (optional)...",
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

public record WizardChoice(string Value, string Display, string Help);
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
    public string HelpTr { get; set; } = "";
    public string HelpEn { get; set; } = "";
    public WizardFieldType FieldType { get; set; }
    public bool AllowOther { get; set; }
    public bool AllowItemNotes { get; set; }
    public int SortOrder { get; set; }
    public string? ConditionalOnFieldKey { get; set; }
    public string? ConditionalHiddenValue { get; set; }
    public List<WizardOptionText> Options { get; set; } = [];

    public string Label(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(LabelEn) ? LabelEn : LabelTr;

    public string Help(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(HelpEn) ? HelpEn : HelpTr;
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
    public Dictionary<string, Dictionary<string, string>> ItemNotes { get; set; } = new();

    public string ExtraNotes { get; set; } = "";
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Models/WizardOptionText.cs') -Content @'
namespace PromptBuilder.Models;

public class WizardOptionText
{
    public string Tr { get; set; } = "";
    public string En { get; set; } = "";
    public string HelpTr { get; set; } = "";
    public string HelpEn { get; set; } = "";

    public string For(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(En) ? En : Tr;

    public string HelpFor(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(HelpEn) ? HelpEn : HelpTr;
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
            SELECT FieldId, FieldKey, Label, LabelEn, Help, HelpEn, FieldType, AllowOther,
                   AllowItemNotes, SortOrder, ConditionalOnFieldKey, ConditionalHiddenValue
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
                    HelpTr = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    HelpEn = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    FieldType = Enum.Parse<WizardFieldType>(reader.GetString(6)),
                    AllowOther = reader.GetBoolean(7),
                    AllowItemNotes = reader.GetBoolean(8),
                    SortOrder = reader.GetInt32(9),
                    ConditionalOnFieldKey = reader.IsDBNull(10) ? null : reader.GetString(10),
                    ConditionalHiddenValue = reader.IsDBNull(11) ? null : reader.GetString(11),
                };
                fields.Add((reader.GetInt32(0), definition));
            }
        }

        const string optionSql = """
            SELECT OptionText, OptionTextEn, OptionHelp, OptionHelpEn
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
                    HelpTr = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    HelpEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
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
        var notes = model.ItemNotes.GetValueOrDefault(field.FieldKey, new Dictionary<string, string>());
        var items = new List<string>();

        foreach (var value in selected)
        {
            var option = field.Options.FirstOrDefault(o => o.Tr == value);
            if (option is null) continue;

            var text = option.For(model.Language);
            if (notes.TryGetValue(value, out var note) && !string.IsNullOrWhiteSpace(note))
            {
                text += $" ({note.Trim()})";
            }
            items.Add(text);
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

.field-label-row {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 8px;
}

.field-label-row .field-label {
    margin-bottom: 0;
}

.field-help summary {
    cursor: pointer;
    color: #2f6fed;
    list-style: none;
    width: 18px;
    height: 18px;
    border-radius: 50%;
    border: 1px solid #2f6fed;
    text-align: center;
    line-height: 16px;
    font-size: 0.75rem;
}

.field-help summary::-webkit-details-marker {
    display: none;
}

.field-help[open] summary {
    background: #2f6fed;
    color: #fff;
}

.field-help p {
    margin: 6px 0 0;
    padding: 8px 10px;
    background: #f4f5f7;
    border-radius: 6px;
    color: #5b6270;
    font-weight: 400;
    font-size: 0.85rem;
    max-width: 560px;
}

.option-row {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.item-note-input {
    margin: 0 0 4px 22px;
    padding: 4px 8px;
    border: 1px solid #d3d6dd;
    border-radius: 6px;
    font-size: 0.85rem;
    max-width: 300px;
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
Write-Host "  2) Eski WizardField/WizardOption tablolari varsa (LabelEn/Help kolonlari olmadan"
Write-Host "     olusturulmus) once DROP edin:"
Write-Host "       DROP TABLE IF EXISTS dbo.WizardOption; DROP TABLE IF EXISTS dbo.WizardField;"
Write-Host "  3) sql/promptbuilder_schema.sql'i o SQL Server veritabaninda calistirin"
Write-Host "     (tablolari olusturur ve sorulari/secenekleri/yardim metinlerini bir kere doldurur)."
Write-Host "  4) dotnet run --project $projectDir  (varsayilan adres: http://localhost:5140)"
