<#
.SYNOPSIS
    FisSayilari.Sync projesini (dosyalari) sifirdan olusturur.
.DESCRIPTION
    Bu script, repo klonlamadan, gerekli tum kaynak dosyalarini
    (csproj, appsettings.json, *.cs, sql/schema.sql) calistigi dizinin
    altina yazar. Ardindan "dotnet build" ile derlemeyi dener.
.EXAMPLE
    .\scaffold-project.ps1
#>

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$projectDir = Join-Path $root 'src/FisSayilari.Sync'
$sqlDir = Join-Path $root 'sql'

New-Item -ItemType Directory -Force -Path $projectDir | Out-Null
New-Item -ItemType Directory -Force -Path $sqlDir | Out-Null

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

Write-ProjectFile -Path (Join-Path $projectDir 'FisSayilari.Sync.csproj') -Content @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>FisSayilari.Sync</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.2" />
    <PackageReference Include="Microsoft.Playwright" Version="1.48.*" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
'@

Write-ProjectFile -Path (Join-Path $projectDir 'appsettings.json') -Content @'
{
  "ConnectionStrings": {
    "FisDb": ""
  },
  "Grafana": {
    "BaseUrl": "https://ztgrafana.zb",
    "DatasourceUid": "",
    "Timezone": "Europe/Istanbul",
    "InfluxDbName": "test",
    "ApiToken": "",
    "Headless": true
  }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'GrafanaOptions.cs') -Content @'
namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";

    // Oturumu kurmak icin once tarayicida acilan dashboard sayfasi.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1";

    // InfluxDB datasource'unun proxy sorgusunda bekledigi "db" query parametresi.
    public string InfluxDbName { get; set; } = "test";

    // Kalici/dogru cozum: Grafana'da olusturulan bir Service Account Token.
    // Doluysa "Authorization: Bearer <ApiToken>" ile duz HTTP istegi atilir, tarayici hic acilmaz.
    public string ApiToken { get; set; } = "";

    // ApiToken bos oldugunda kullanilan yol: Playwright ile gercek bir Edge penceresi acilip
    // (playwright-profile/ klasorunde saklanan kalici profille) dashboard sayfasina gidilir,
    // boylece o tarayicinin SSO/Windows oturumu kullanilir. Ilk calistirmada SSO otomatik
    // tamamlanmazsa Headless=false ile pencereyi gorup elle giris yapabilirsiniz; sonraki
    // calistirmalarda ayni profil sayesinde Headless=true yeterli olur.
    public bool Headless { get; set; } = true;
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'GrafanaInfluxClient.cs') -Content @'
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Playwright;

namespace FisSayilari.Sync;

public sealed record GunlukFisSayisi(DateOnly Gun, string Kanal, long ToplamFisSayisi);

// Ziraat Bankasi Kanal Fis Sayilari dashboard'undaki (UN0bbgwnz, panel 24) 6 InfluxQL sorgusuyla
// eslesen olcum adlari. Inspect > JSON > "DataFrame JSON (from Query)" ciktisindan alindi.
public sealed class GrafanaInfluxClient : IAsyncDisposable
{
    private static readonly (string Measurement, string Kanal)[] Kanallar =
    [
        ("MOBIL_FIS_SAY", "Mobil"),
        ("SUBE_FIS_SAY", "Sube"),
        ("INTERNET_FIS_SAY", "Internet"),
        ("ATM_FIS_SAY", "Atm"),
        ("ATMTAM_FIS_SAY", "AtmTam"),
        ("POS_FIS_SAY", "Pos"),
    ];

    private const string RetentionPolicy = "autogen";
    private const string Alan = "ADET";

    private readonly GrafanaOptions _options;
    private readonly TimeZoneInfo _timeZone;

    private HttpClient? _tokenHttpClient;
    private IPlaywright? _playwright;
    private IBrowserContext? _browserContext;

    public GrafanaInfluxClient(GrafanaOptions options)
    {
        _options = options;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Timezone);
    }

    // fromDay/toDay dahil (inclusive) araliktaki her gun ve her kanal icin InfluxDB'ye
    // GROUP BY time(1d) ile toplatilmis fis sayisini doner. Dakikalik veri hic bu tarafa cekilmez.
    // Panelin kendisi gibi, 6 kanalin sorgusunu tek istekte (noktali virgulle ayrilmis) gonderiyoruz.
    public async Task<IReadOnlyList<GunlukFisSayisi>> GetGunlukToplamlarAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken ct = default)
    {
        var startMs = IstanbulGunBasiUtcMs(fromDay);
        var endMs = IstanbulGunBasiUtcMs(toDay.AddDays(1)); // ust sinir haric (exclusive)

        var combinedQuery = string.Join(";", Kanallar.Select(k =>
            $"SELECT SUM(\"{Alan}\") FROM \"{RetentionPolicy}\".\"{k.Measurement}\" " +
            $"WHERE time >= {startMs}ms AND time < {endMs}ms " +
            $"GROUP BY time(1d) tz('{_options.Timezone}')"));

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/datasources/proxy/uid/{_options.DatasourceUid}/query" +
                  $"?db={Uri.EscapeDataString(_options.InfluxDbName)}&epoch=ms&q={Uri.EscapeDataString(combinedQuery)}";

        var (status, body) = string.IsNullOrWhiteSpace(_options.ApiToken)
            ? await FetchViaBrowserAsync(url, ct)
            : await FetchViaHttpClientAsync(url, ct);

        if (status is < 200 or >= 300)
        {
            throw new InvalidOperationException($"Grafana proxy istegi basarisiz (HTTP {status}): {body}");
        }

        return ParseGunlukSeriler(body).ToList();
    }

    // Kalici/dogru yol: Service Account Token varsa duz HTTP yeterli, tarayici hic gerekmez.
    private async Task<(int Status, string Body)> FetchViaHttpClientAsync(string url, CancellationToken ct)
    {
        if (_tokenHttpClient is null)
        {
            _tokenHttpClient = new HttpClient();
            _tokenHttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        }

        using var response = await _tokenHttpClient.GetAsync(url, ct);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    // Token yoksa: gercek bir tarayici (sistemde kurulu Edge) kalici bir profille aciliyor,
    // dashboard sayfasina gidip SSO/Windows oturumunu kuruyor, sonra ayni oturumun
    // cerezlerini paylasan APIRequest ile bizim InfluxQL sorgumuzu atiyoruz.
    private async Task<(int Status, string Body)> FetchViaBrowserAsync(string url, CancellationToken ct)
    {
        if (_browserContext is null)
        {
            _playwright = await Playwright.CreateAsync();

            var profileDir = Path.Combine(AppContext.BaseDirectory, "playwright-profile");
            _browserContext = await _playwright.Chromium.LaunchPersistentContextAsync(profileDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Channel = "msedge",
                    Headless = _options.Headless,
                    ExtraHTTPHeaders = new Dictionary<string, string> { ["x-grafana-org-id"] = "1" },
                });

            var dashboardUrl = $"{_options.BaseUrl.TrimEnd('/')}{_options.DashboardPath}";
            var page = await _browserContext.NewPageAsync();
            await page.GotoAsync(dashboardUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.CloseAsync();
        }

        var response = await _browserContext.APIRequest.GetAsync(url);
        var body = await response.TextAsync();
        return (response.Status, body);
    }

    // InfluxDB, noktali virgulle ayrilmis her SELECT icin "results" dizisinde ayri bir eleman doner,
    // sirasi gonderilen sorgu sirasiyla (yani Kanallar dizisiyle) ayni.
    private IEnumerable<GunlukFisSayisi> ParseGunlukSeriler(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var results = doc.RootElement.GetProperty("results");

        for (var i = 0; i < results.GetArrayLength() && i < Kanallar.Length; i++)
        {
            var kanal = Kanallar[i].Kanal;
            if (!results[i].TryGetProperty("series", out var seriesArray))
                continue; // bu kanalda bu araliktaki hicbir gunde veri yok

            foreach (var series in seriesArray.EnumerateArray())
            {
                var values = series.GetProperty("values");
                foreach (var row in values.EnumerateArray())
                {
                    var epochMs = row[0].GetInt64();
                    // sum(ADET) veri olmayan bir gun icin null donebilir
                    var toplam = row[1].ValueKind == JsonValueKind.Null ? 0L : row[1].GetInt64();

                    var utc = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
                    var localGunBaslangici = TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
                    yield return new GunlukFisSayisi(DateOnly.FromDateTime(localGunBaslangici), kanal, toplam);
                }
            }
        }
    }

    private long IstanbulGunBasiUtcMs(DateOnly gun)
    {
        var localMidnight = DateTime.SpecifyKind(gun.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, _timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browserContext is not null)
            await _browserContext.CloseAsync();
        _playwright?.Dispose();
        _tokenHttpClient?.Dispose();
    }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'FisGunlukRepository.cs') -Content @'
using Microsoft.Data.SqlClient;

namespace FisSayilari.Sync;

public sealed class FisGunlukRepository
{
    private readonly string _connectionString;

    public FisGunlukRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task UpsertAsync(IReadOnlyList<GunlukFisSayisi> satirlar, CancellationToken ct = default)
    {
        if (satirlar.Count == 0) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        const string merge = """
            MERGE dbo.FisGunlukOzet AS hedef
            USING (SELECT @Tarih AS Tarih, @Kanal AS Kanal) AS kaynak
                ON hedef.Tarih = kaynak.Tarih AND hedef.Kanal = kaynak.Kanal
            WHEN MATCHED THEN
                UPDATE SET ToplamFisSayisi = @ToplamFisSayisi, GuncellemeZamani = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Tarih, Kanal, ToplamFisSayisi, GuncellemeZamani)
                VALUES (@Tarih, @Kanal, @ToplamFisSayisi, SYSUTCDATETIME());
            """;

        foreach (var satir in satirlar)
        {
            await using var command = new SqlCommand(merge, connection, transaction);
            command.Parameters.AddWithValue("@Tarih", satir.Gun.ToDateTime(TimeOnly.MinValue));
            command.Parameters.AddWithValue("@Kanal", satir.Kanal);
            command.Parameters.AddWithValue("@ToplamFisSayisi", satir.ToplamFisSayisi);
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'Program.cs') -Content @'
using FisSayilari.Sync;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var grafanaOptions = config.GetSection("Grafana").Get<GrafanaOptions>()
    ?? throw new InvalidOperationException("appsettings.json icinde 'Grafana' bolumu eksik.");
var connectionString = config.GetConnectionString("FisDb");

if (string.IsNullOrWhiteSpace(grafanaOptions.DatasourceUid))
    throw new InvalidOperationException("appsettings.json: Grafana:DatasourceUid bos birakilamaz.");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("appsettings.json: ConnectionStrings:FisDb bos birakilamaz.");

// Ilk deneme: sadece bugunu cek ve DB'ye yaz.
var bugun = DateOnly.FromDateTime(DateTime.Now);

// ApiToken bossa, GrafanaInfluxClient ilk cagrida sistemde kurulu Edge'i acip
// (playwright-profile/ klasorundeki kalici oturumla) SSO'yu tarayici uzerinden halleder.
await using var grafanaClient = new GrafanaInfluxClient(grafanaOptions);
var repository = new FisGunlukRepository(connectionString);

Console.WriteLine($"{bugun:yyyy-MM-dd} icin fis sayilari Grafana/InfluxDB proxy'sinden cekiliyor...");
var gunlukToplamlar = await grafanaClient.GetGunlukToplamlarAsync(bugun, bugun);

foreach (var satir in gunlukToplamlar)
    Console.WriteLine($"  {satir.Gun:yyyy-MM-dd}  {satir.Kanal,-10}  {satir.ToplamFisSayisi}");

if (gunlukToplamlar.Count == 0)
{
    Console.WriteLine("Hicbir kanaldan veri donmedi (gun henuz cok erken olabilir ya da sorgu/uid hatali).");
    return;
}

await repository.UpsertAsync(gunlukToplamlar);
Console.WriteLine($"{gunlukToplamlar.Count} satir dbo.FisGunlukOzet tablosuna yazildi.");
'@

Write-ProjectFile -Path (Join-Path $sqlDir 'schema.sql') -Content @'
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
Write-Host "appsettings.json icindeki ConnectionStrings:FisDb ve Grafana:DatasourceUid alanlarini doldurup"
Write-Host "'dotnet run --project $projectDir' ile calistirabilirsiniz."
Write-Host ""
Write-Host "Not: appsettings.json'da Grafana:ApiToken bossa, ilk calistirmada bir Edge penceresi"
Write-Host "acilir (Headless=false yapip calistirirsaniz gorursunuz). SSO otomatik tamamlanmazsa"
Write-Host "o pencerede elle giris yapin; oturum playwright-profile/ klasorunde saklanir ve"
Write-Host "sonraki calistirmalarda (Headless=true) tekrar kullanilir."
