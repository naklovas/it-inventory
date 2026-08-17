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

// UseDefaultCredentials: Grafana'nin onunde SSO/Windows Integrated Auth (NTLM/Kerberos) varsa,
// bu programi calistiran domain kullanicisinin kimligini otomatik gonderir - tarayicinizdaki
// sessiz SSO'nun aynisi. grafana_session cerezi bu adimdan sonra kurulur.
using var httpClientHandler = new HttpClientHandler { UseDefaultCredentials = true };
using var httpClient = new HttpClient(httpClientHandler);
httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
httpClient.DefaultRequestHeaders.Add("x-grafana-org-id", "1");
httpClient.DefaultRequestHeaders.Add(
    "Referer", $"{grafanaOptions.BaseUrl.TrimEnd('/')}{grafanaOptions.DashboardPath}");

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
