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

    public GrafanaInfluxClient(HttpClient httpClient, GrafanaOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Timezone);
    }

    // fromDay/toDay dahil (inclusive) araliktaki her gun ve her kanal icin InfluxDB'ye
    // GROUP BY time(1d) ile toplatilmis fis sayisini doner. Dakikalik veri hic bu tarafa cekilmez.
    public async Task<IReadOnlyList<GunlukFisSayisi>> GetGunlukToplamlarAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken ct = default)
    {
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
