using Kesinti.Core.Configuration;
using Kesinti.Core.Data;
using Kesinti.Core.Models;
using Kesinti.Ingest.Docx;

var configuration = AppConfig.Load(AppContext.BaseDirectory, args);
var dbOptions = AppConfig.GetDatabase(configuration);

var klasor = args.FirstOrDefault(a => !a.StartsWith("--"))
    ?? configuration["Ingest:KaynakKlasor"]
    ?? throw new InvalidOperationException(
        "Islem yapilacak klasor belirtilmedi. Komut satirinda klasor yolu verin veya 'Ingest:KaynakKlasor' ayarini tanimlayin.");

if (!Directory.Exists(klasor))
{
    Console.Error.WriteLine($"Klasor bulunamadi: {klasor}");
    return 1;
}

var connectionFactory = new SqlConnectionFactory(dbOptions);
var repository = new KesintiRepository(connectionFactory);

var docxDosyalari = Directory.EnumerateFiles(klasor, "*.docx", SearchOption.AllDirectories)
    .Where(f => !Path.GetFileName(f).StartsWith('~')) // Word kilit/gecici dosyalarini atla
    .OrderBy(f => f)
    .ToList();

Console.WriteLine($"{docxDosyalari.Count} .docx dosyasi bulundu: {klasor}");

var islenen = 0;
var atlanan = 0;
var hatali = 0;

foreach (var dosyaYolu in docxDosyalari)
{
    try
    {
        var hash = FileHasher.ComputeSha256(dosyaYolu);

        if (await repository.DosyaZatenIslendiMiAsync(hash))
        {
            Console.WriteLine($"[ATLA]  {dosyaYolu} (daha once islendi)");
            atlanan++;
            continue;
        }

        var hamMetin = DocxReader.ReadPlainText(dosyaYolu);
        var alanlar = FieldExtractor.Extract(hamMetin);
        var formatDurumu = alanlar.StandardaUyuyorMu ? FormatDurumu.Standart : FormatDurumu.StandartDisi;

        var dokuman = new KesintiDokuman
        {
            DosyaYolu = dosyaYolu,
            DosyaHash = hash,
            HamMetin = hamMetin,
            FormatDurumu = formatDurumu,
        };
        var kesintiDokumanId = await repository.DokumanEkleAsync(dokuman);

        var kayit = new KesintiKayit
        {
            KesintiDokumanId = kesintiDokumanId,
            Tarih = alanlar.Tarih,
            SureDakika = alanlar.SureDakika,
            EtkilenenSistemler = alanlar.EtkilenenSistemler,
            Neden = alanlar.Neden,
            AlinanOnlemler = alanlar.AlinanOnlemler,
            SiniflandirmaDurumu = SiniflandirmaDurumu.Bekliyor,
        };
        await repository.KayitEkleAsync(kayit);

        Console.WriteLine($"[OK]    {dosyaYolu} -> {formatDurumu}");
        islenen++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[HATA]  {dosyaYolu}: {ex.Message}");
        hatali++;
    }
}

Console.WriteLine();
Console.WriteLine($"Islenen: {islenen}, Atlanan (tekrar): {atlanan}, Hatali: {hatali}");

return hatali > 0 ? 1 : 0;
