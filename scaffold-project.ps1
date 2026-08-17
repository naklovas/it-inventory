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
    "Timezone": "Europe/Istanbul"
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

    // Anonim erisimde Grafana, sayfa ilk yuklendiginde bir oturum cerezi veriyor;
    // API cagrilari bu cerez olmadan 401 donebiliyor. Bu yuzden once bu sayfaya
    // bir "isinma" istegi atip cerezi aliyoruz, sonra ayni HttpClient ile proxy'yi cagiriyoruz.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1";
}
'@

Write-ProjectFile -Path (Join-Path $projectDir 'GrafanaInfluxClient.cs') -Content @'
using System.Text.Json;

namespace FisSayilari.Sync;

public sealed record GunlukFisSayisi(DateOnly Gun, string Kanal, long ToplamFisSayisi);

// Ziraat Bankasi Kanal Fis Sayilari dashboard'undaki (UN0bbgwnz, panel 24) 6 InfluxQL sorgusuyla
// eslesen olcum adlari. Inspect > JSON > "DataFrame JSON (from Query)" ciktisindan alindi.
public sealed class GrafanaInfluxClient
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

    private readonly HttpClient _httpClient;
    private readonly GrafanaOptions _options;
    private readonly TimeZoneInfo _timeZone;

    private bool _oturumIsindi;

    public GrafanaInfluxClient(HttpClient httpClient, GrafanaOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Timezone);
    }

    // Anonim erisimde Grafana, dashboard sayfasi ilk yuklendiginde bir oturum cerezi veriyor;
    // API'ye o cerez olmadan gidilirse 401 donuyor. HttpClient'in varsayilan handler'i
    // cerezleri kendiliginden tasidigi icin, bu istekten sonraki tum cagrilar ayni cerezi kullanir.
    private async Task OturumIsitAsync(CancellationToken ct)
    {
        if (_oturumIsindi) return;

        var url = $"{_options.BaseUrl.TrimEnd('/')}{_options.DashboardPath}";
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        _oturumIsindi = true;
    }

    // fromDay/toDay dahil (inclusive) araliktaki her gun ve her kanal icin InfluxDB'ye
    // GROUP BY time(1d) ile toplatilmis fis sayisini doner. Dakikalik veri hic bu tarafa cekilmez.
    public async Task<IReadOnlyList<GunlukFisSayisi>> GetGunlukToplamlarAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken ct = default)
    {
        await OturumIsitAsync(ct);

        var startMs = IstanbulGunBasiUtcMs(fromDay);
        var endMs = IstanbulGunBasiUtcMs(toDay.AddDays(1)); // ust sinir haric (exclusive)

        var sonuc = new List<GunlukFisSayisi>();
        foreach (var (measurement, kanal) in Kanallar)
        {
            var query = $"SELECT SUM(\"{Alan}\") FROM \"{RetentionPolicy}\".\"{measurement}\" " +
                        $"WHERE time >= {startMs}ms AND time < {endMs}ms " +
                        $"GROUP BY time(1d) tz('{_options.Timezone}')";

            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/datasources/proxy/uid/{_options.DatasourceUid}/query" +
                      $"?epoch=ms&q={Uri.EscapeDataString(query)}";

            using var response = await _httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Grafana proxy istegi basarisiz ({kanal}, HTTP {(int)response.StatusCode}): {body}");
            }

            sonuc.AddRange(ParseGunlukSeriler(body, kanal));
        }

        return sonuc;
    }

    private IEnumerable<GunlukFisSayisi> ParseGunlukSeriler(string responseJson, string kanal)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var results = doc.RootElement.GetProperty("results");
        if (results.GetArrayLength() == 0)
            yield break;

        var firstResult = results[0];
        if (!firstResult.TryGetProperty("series", out var seriesArray))
            yield break; // bu araliktaki hicbir gunde veri yok

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

    private long IstanbulGunBasiUtcMs(DateOnly gun)
    {
        var localMidnight = DateTime.SpecifyKind(gun.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, _timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
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

using var httpClient = new HttpClient();
var grafanaClient = new GrafanaInfluxClient(httpClient, grafanaOptions);
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
