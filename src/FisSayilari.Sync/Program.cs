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

// Tarih araligi appsettings.json > Cekim bolumunden okunur (BaslangicGunu/BitisGunu bos
// birakilirsa bugun kullanilir). Komut satiri argumani verilirse (dotnet run -- 2026-08-10
// [2026-08-10]) settings'i gecersiz kilar.
var baslangicStr = config["Cekim:BaslangicGunu"];
var bitisStr = config["Cekim:BitisGunu"];

DateOnly fromDay, toDay;
if (args.Length >= 1)
{
    fromDay = DateOnly.Parse(args[0]);
    toDay = args.Length >= 2 ? DateOnly.Parse(args[1]) : fromDay;
}
else if (!string.IsNullOrWhiteSpace(baslangicStr))
{
    fromDay = DateOnly.Parse(baslangicStr);
    toDay = !string.IsNullOrWhiteSpace(bitisStr) ? DateOnly.Parse(bitisStr) : fromDay;
}
else
{
    fromDay = toDay = DateOnly.FromDateTime(DateTime.Now);
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
