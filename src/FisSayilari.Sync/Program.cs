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

// Kullanim: dotnet run                     -> bugun
//          dotnet run -- 2026-08-10        -> tek gun
//          dotnet run -- 2026-08-01 2026-08-10 -> tarih araligi (dahil)
DateOnly fromDay, toDay;
switch (args.Length)
{
    case 0:
        fromDay = toDay = DateOnly.FromDateTime(DateTime.Now);
        break;
    case 1:
        fromDay = toDay = DateOnly.Parse(args[0]);
        break;
    default:
        fromDay = DateOnly.Parse(args[0]);
        toDay = DateOnly.Parse(args[1]);
        break;
}

if (fromDay > toDay)
    throw new InvalidOperationException("Baslangic tarihi bitis tarihinden sonra olamaz.");

using var grafanaClient = new GrafanaInfluxClient(grafanaOptions);
var repository = new FisGunlukRepository(connectionString);

Console.WriteLine($"{fromDay:yyyy-MM-dd} - {toDay:yyyy-MM-dd} araligi icin fis sayilari Grafana/InfluxDB proxy'sinden cekiliyor...");
var gunlukToplamlar = await grafanaClient.GetGunlukToplamlarAsync(fromDay, toDay);

foreach (var satir in gunlukToplamlar)
    Console.WriteLine($"  {satir.Gun:yyyy-MM-dd}  {satir.Kanal,-10}  {satir.ToplamFisSayisi}");

if (gunlukToplamlar.Count == 0)
{
    Console.WriteLine("Hicbir kanaldan veri donmedi (secilen aralikta veri olmayabilir ya da sorgu/uid hatali).");
    return;
}

await repository.UpsertAsync(gunlukToplamlar);
Console.WriteLine($"{gunlukToplamlar.Count} satir dbo.FisGunlukOzet tablosuna yazildi.");
