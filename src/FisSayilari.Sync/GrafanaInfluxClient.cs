using System.Net.Http.Headers;
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

    // Kimlik dogrulamayi bir kez kurar. Oncelik sirasi:
    // 1) ApiToken (Service Account Token) - kalici/dogru cozum, Bearer header yeterli.
    // 2) SessionCookie - tarayicidan elle kopyalanan gecici cerez, hizli test icin.
    // 3) Hicbiri yoksa: dashboard sayfasina bir istek atip SSO/Windows auth ile
    //    kurulacak oturum cerezini HttpClient'in varsayilan cerez yonetimine birakir.
    private async Task OturumIsitAsync(CancellationToken ct)
    {
        if (_oturumIsindi) return;

        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        }
        else if (!string.IsNullOrWhiteSpace(_options.SessionCookie))
        {
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"grafana_session={_options.SessionCookie}");
        }
        else
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}{_options.DashboardPath}";
            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
        }

        _oturumIsindi = true;
    }

    // fromDay/toDay dahil (inclusive) araliktaki her gun ve her kanal icin InfluxDB'ye
    // GROUP BY time(1d) ile toplatilmis fis sayisini doner. Dakikalik veri hic bu tarafa cekilmez.
    // Panelin kendisi gibi, 6 kanalin sorgusunu tek istekte (noktali virgulle ayrilmis) gonderiyoruz.
    public async Task<IReadOnlyList<GunlukFisSayisi>> GetGunlukToplamlarAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken ct = default)
    {
        await OturumIsitAsync(ct);

        var startMs = IstanbulGunBasiUtcMs(fromDay);
        var endMs = IstanbulGunBasiUtcMs(toDay.AddDays(1)); // ust sinir haric (exclusive)

        var combinedQuery = string.Join(";", Kanallar.Select(k =>
            $"SELECT SUM(\"{Alan}\") FROM \"{RetentionPolicy}\".\"{k.Measurement}\" " +
            $"WHERE time >= {startMs}ms AND time < {endMs}ms " +
            $"GROUP BY time(1d) tz('{_options.Timezone}')"));

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/datasources/proxy/uid/{_options.DatasourceUid}/query" +
                  $"?db={Uri.EscapeDataString(_options.InfluxDbName)}&epoch=ms&q={Uri.EscapeDataString(combinedQuery)}";

        using var response = await _httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Grafana proxy istegi basarisiz (HTTP {(int)response.StatusCode}): {body}");
        }

        return ParseGunlukSeriler(body).ToList();
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
}
