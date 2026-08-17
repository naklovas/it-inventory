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

            // Sayfayi kasten kapatmiyoruz: kalici context'te tek sayfa kalirsa tarayici sureci
            // de kapaniyor (Playwright'in bilinen davranisi), sonraki APIRequest cagrisi
            // "Target ... has been closed" hatasi veriyor.
            var dashboardUrl = $"{_options.BaseUrl.TrimEnd('/')}{_options.DashboardPath}";
            var page = await _browserContext.NewPageAsync();
            await page.GotoAsync(dashboardUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
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
