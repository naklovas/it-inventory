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
    "SessionCookie": ""
  },
  "Cekim": {
    "Baslangic": "",
    "Bitis": "",
    "AralikDakika": 60
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

    // Sadece "Referer" header'ini olusturmak icin kullaniliyor - calisan tarayici istegiyle
    // birebir ayni gorunmesi icin.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1&viewPanel=24";

    // InfluxDB datasource'unun proxy sorgusunda bekledigi "db" query parametresi.
    public string InfluxDbName { get; set; } = "test";

    // Kalici/dogru cozum: Grafana'da olusturulan bir Service Account Token.
    // Doluysa "Authorization: Bearer <ApiToken>" ile istek atilir.
    public string ApiToken { get; set; } = "";

    // Gecici cozum: tarayicidan (F12 > Network > istegin "Cookie" header'i, ya da
    // curl komutundaki -b "..." icerigi) kopyalanan TAM cerez metni, oldugu gibi -
    // ornek: "grafana_session=xxx; grafana_session_expiry=yyy". Sadece grafana_session
    // gonderip grafana_session_expiry'i atlamak 401'e yol aciyor, ikisi birlikte gerekli.
    // Sadece hizli test icin - bir sure sonra suresi dolar. ApiToken bosken, SessionCookie
    // doluysa bu kullanilir.
    public string SessionCookie { get; set; } = "";
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'GrafanaInfluxClient.cs') -Content @'
using System.Net.Http.Headers;
using System.Text.Json;

namespace FisSayilari.Sync;

public sealed record FisSayisiKaydi(DateTime Zaman, string Kanal, long ToplamFisSayisi);

// Ziraat Bankasi Kanal Fis Sayilari dashboard'undaki (UN0bbgwnz, panel 24) 6 InfluxQL sorgusuyla
// eslesen olcum adlari. Inspect > JSON > "DataFrame JSON (from Query)" ciktisindan alindi.
public sealed class GrafanaInfluxClient : IDisposable
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
    private readonly HttpClient _httpClient = new();

    public GrafanaInfluxClient(GrafanaOptions options)
    {
        _options = options;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Timezone);

        // Calisan tarayici istegiyle (F12'den yakalanan) birebir ayni gorunmesi icin:
        // varsayilan HttpClient User-Agent gondermiyor, x-grafana-org-id ve Referer'i de
        // eklemiyordu - bir WAF/guvenlik katmani bunlari kontrol ediyor olabilir.
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("x-grafana-org-id", "1");
        _httpClient.DefaultRequestHeaders.Add(
            "Referer", $"{options.BaseUrl.TrimEnd('/')}{options.DashboardPath}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/128.0.0.0 Safari/537.36 Edg/128.0.0.0");

        // Kurumsal SSO/Windows Integrated Auth (401) ve tarayici otomasyonu (IT politikasiyla
        // Edge'de remote debugging kapali) ikisi de bu ortamda calismiyor. Geriye kalan tek
        // saglam yol: bir Grafana Service Account Token. SessionCookie sadece hizli, tek
        // seferlik test icindir - suresi dolar, kalici script bunu kullanmamali.
        if (!string.IsNullOrWhiteSpace(options.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiToken);
            Console.WriteLine("[auth] ApiToken kullaniliyor (uzunluk: " + options.ApiToken.Length + ")");
        }
        else if (!string.IsNullOrWhiteSpace(options.SessionCookie))
        {
            // options.SessionCookie tarayicidan kopyalanan TAM cerez metni
            // ("grafana_session=...; grafana_session_expiry=...") - oldugu gibi gonderiliyor.
            _httpClient.DefaultRequestHeaders.Add("Cookie", options.SessionCookie);
            Console.WriteLine("[auth] SessionCookie kullaniliyor (uzunluk: " + options.SessionCookie.Length + "): " +
                options.SessionCookie);
        }
        else
        {
            throw new InvalidOperationException(
                "appsettings.json: Grafana:ApiToken (kalici) ya da Grafana:SessionCookie " +
                "(gecici test) alanlarindan biri doldurulmali.");
        }
    }

    // baslangic/bitis (yerel saat, Istanbul) araligindaki her zaman dilimi ve her kanal icin
    // InfluxDB'ye GROUP BY time(aralikDakika) ile toplatilmis fis sayisini doner.
    // aralikDakika=1440 gunluk, 60 saatlik, 15 15-dakikalik dilimler verir.
    // Panelin kendisi gibi, 6 kanalin sorgusunu tek istekte (noktali virgulle ayrilmis) gonderiyoruz.
    public async Task<IReadOnlyList<FisSayisiKaydi>> GetToplamlarAsync(
        DateTime baslangic, DateTime bitis, int aralikDakika, CancellationToken ct = default)
    {
        var startMs = IstanbulZamanUtcMs(baslangic);
        var endMs = IstanbulZamanUtcMs(bitis);

        var combinedQuery = string.Join(";", Kanallar.Select(k =>
            $"SELECT SUM(\"{Alan}\") FROM \"{RetentionPolicy}\".\"{k.Measurement}\" " +
            $"WHERE time >= {startMs}ms AND time < {endMs}ms " +
            $"GROUP BY time({aralikDakika}m) tz('{_options.Timezone}')"));

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/datasources/proxy/uid/{_options.DatasourceUid}/query" +
                  $"?db={Uri.EscapeDataString(_options.InfluxDbName)}&epoch=ms&q={Uri.EscapeDataString(combinedQuery)}";

        using var response = await _httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Grafana proxy istegi basarisiz (HTTP {(int)response.StatusCode}): {body}");
        }

        return ParseSeriler(body).ToList();
    }

    // InfluxDB, noktali virgulle ayrilmis her SELECT icin "results" dizisinde ayri bir eleman doner,
    // sirasi gonderilen sorgu sirasiyla (yani Kanallar dizisiyle) ayni.
    private IEnumerable<FisSayisiKaydi> ParseSeriler(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var results = doc.RootElement.GetProperty("results");

        for (var i = 0; i < results.GetArrayLength() && i < Kanallar.Length; i++)
        {
            var kanal = Kanallar[i].Kanal;
            if (!results[i].TryGetProperty("series", out var seriesArray))
                continue; // bu kanalda bu araliktaki hicbir dilimde veri yok

            foreach (var series in seriesArray.EnumerateArray())
            {
                var values = series.GetProperty("values");
                foreach (var row in values.EnumerateArray())
                {
                    var epochMs = row[0].GetInt64();
                    // sum(ADET) veri olmayan bir dilim icin null donebilir
                    var toplam = row[1].ValueKind == JsonValueKind.Null ? 0L : row[1].GetInt64();

                    var utc = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
                    var localZaman = TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
                    yield return new FisSayisiKaydi(localZaman, kanal, toplam);
                }
            }
        }
    }

    private long IstanbulZamanUtcMs(DateTime yerelZaman)
    {
        var yerel = DateTime.SpecifyKind(yerelZaman, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(yerel, _timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    public void Dispose() => _httpClient.Dispose();
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'FisSayilariRepository.cs') -Content @'
using Microsoft.Data.SqlClient;

namespace FisSayilari.Sync;

public sealed class FisSayilariRepository
{
    private readonly string _connectionString;

    public FisSayilariRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task UpsertAsync(IReadOnlyList<FisSayisiKaydi> satirlar, CancellationToken ct = default)
    {
        if (satirlar.Count == 0) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        const string merge = """
            MERGE dbo.FisSayilariOzet AS hedef
            USING (SELECT @Zaman AS Zaman, @Kanal AS Kanal) AS kaynak
                ON hedef.Zaman = kaynak.Zaman AND hedef.Kanal = kaynak.Kanal
            WHEN MATCHED THEN
                UPDATE SET ToplamFisSayisi = @ToplamFisSayisi, GuncellemeZamani = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Zaman, Kanal, ToplamFisSayisi, GuncellemeZamani)
                VALUES (@Zaman, @Kanal, @ToplamFisSayisi, SYSUTCDATETIME());
            """;

        foreach (var satir in satirlar)
        {
            await using var command = new SqlCommand(merge, connection, transaction);
            command.Parameters.AddWithValue("@Zaman", satir.Zaman);
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

// Zaman araligi appsettings.json > Cekim bolumunden okunur (Baslangic/Bitis formati
// "yyyy-MM-dd HH:mm", bos birakilirsa bugun 00:00 - simdi kullanilir). AralikDakika,
// GROUP BY dilim genisligi: 1440 gunluk, 60 saatlik, 15 15-dakikalik. Komut satiri
// argumani verilirse (dotnet run -- "2026-08-10 09:00" "2026-08-10 18:00") settings'i
// gecersiz kilar.
var baslangicStr = config["Cekim:Baslangic"];
var bitisStr = config["Cekim:Bitis"];
var aralikDakikaStr = config["Cekim:AralikDakika"];

DateTime baslangic, bitis;
if (args.Length >= 1)
{
    baslangic = DateTime.Parse(args[0]);
    bitis = args.Length >= 2 ? DateTime.Parse(args[1]) : DateTime.Now;
}
else if (!string.IsNullOrWhiteSpace(baslangicStr))
{
    baslangic = DateTime.Parse(baslangicStr);
    bitis = !string.IsNullOrWhiteSpace(bitisStr) ? DateTime.Parse(bitisStr) : DateTime.Now;
}
else
{
    baslangic = DateTime.Today;
    bitis = DateTime.Now;
}

var aralikDakika = string.IsNullOrWhiteSpace(aralikDakikaStr) ? 60 : int.Parse(aralikDakikaStr);

if (baslangic > bitis)
    throw new InvalidOperationException("Baslangic, bitisten sonra olamaz.");

using var grafanaClient = new GrafanaInfluxClient(grafanaOptions);
var repository = new FisSayilariRepository(connectionString);

Console.WriteLine(
    $"{baslangic:yyyy-MM-dd HH:mm} - {bitis:yyyy-MM-dd HH:mm} araligi, {aralikDakika} dakikalik " +
    "dilimlerle Grafana/InfluxDB proxy'sinden cekiliyor...");
var kayitlar = await grafanaClient.GetToplamlarAsync(baslangic, bitis, aralikDakika);

foreach (var satir in kayitlar)
    Console.WriteLine($"  {satir.Zaman:yyyy-MM-dd HH:mm}  {satir.Kanal,-10}  {satir.ToplamFisSayisi}");

if (kayitlar.Count == 0)
{
    Console.WriteLine("Hicbir kanaldan veri donmedi (secilen aralikta veri olmayabilir ya da sorgu/uid hatali).");
    return;
}

await repository.UpsertAsync(kayitlar);
Console.WriteLine($"{kayitlar.Count} satir dbo.FisSayilariOzet tablosuna yazildi.");
'@

Write-ProjectFile -Path (Join-Path $sqlDir 'schema.sql') -Content @'
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FisSayilariOzet' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.FisSayilariOzet
    (
        Zaman               DATETIME2       NOT NULL,
        Kanal               NVARCHAR(20)    NOT NULL,
        ToplamFisSayisi     BIGINT          NOT NULL,
        GuncellemeZamani    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FisSayilariOzet PRIMARY KEY (Zaman, Kanal)
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
Write-Host "appsettings.json icindeki ConnectionStrings:FisDb, Grafana:DatasourceUid ve"
Write-Host "Grafana:ApiToken (ya da gecici test icin Grafana:SessionCookie) alanlarini doldurup"
Write-Host "'dotnet run --project $projectDir' ile calistirabilirsiniz."
