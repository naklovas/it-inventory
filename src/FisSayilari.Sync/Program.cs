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
