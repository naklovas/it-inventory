using System.Text.Json;
using Dapper;
using Kesinti.Classify.Classification;
using Kesinti.Core.Configuration;
using Kesinti.Core.Data;
using Kesinti.Core.LiteLlm;
using Kesinti.Core.Models;

var configuration = AppConfig.Load(AppContext.BaseDirectory, args);
var dbOptions = AppConfig.GetDatabase(configuration);

var connectionFactory = new SqlConnectionFactory(dbOptions);
var kesintiRepository = new KesintiRepository(connectionFactory);
var kategoriRepository = new KategoriRepository(connectionFactory);

if (args.Length > 0 && args[0].Equals("duzelt", StringComparison.OrdinalIgnoreCase))
{
    return await ElleDuzeltAsync(args, connectionFactory, kategoriRepository);
}

return await SiniflandirBekleyenleriAsync(configuration, dbOptions, connectionFactory, kesintiRepository, kategoriRepository);

static async Task<int> SiniflandirBekleyenleriAsync(
    Microsoft.Extensions.Configuration.IConfigurationRoot configuration,
    DatabaseOptions dbOptions,
    SqlConnectionFactory connectionFactory,
    KesintiRepository kesintiRepository,
    KategoriRepository kategoriRepository)
{
    var liteLlmOptions = AppConfig.GetLiteLlm(configuration);
    var liteLlmClient = new LiteLlmClient(liteLlmOptions);

    var kategoriler = await kategoriRepository.GetAktifKategorilerAsync();
    if (kategoriler.Count == 0)
    {
        Console.Error.WriteLine("Kategori tablosunda aktif kategori bulunamadi.");
        return 1;
    }

    var kategoriAdlari = kategoriler.Select(k => k.Ad).ToList();
    var schema = ClassificationSchema.Build(kategoriAdlari);
    var systemPrompt = PromptBuilder.SystemPrompt(kategoriAdlari);

    var bekleyenler = await kesintiRepository.GetBekleyenKayitlarAsync();
    Console.WriteLine($"{bekleyenler.Count} bekleyen kayit bulundu.");

    var basarili = 0;
    var hatali = 0;

    foreach (var kayit in bekleyenler)
    {
        try
        {
            var dokuman = await kesintiRepository.GetDokumanAsync(kayit.KesintiDokumanId)
                ?? throw new InvalidOperationException($"KesintiDokumanId={kayit.KesintiDokumanId} bulunamadi.");

            var userPrompt = PromptBuilder.UserPrompt(kayit, dokuman);
            var modelCikisiJson = await liteLlmClient.GetStructuredCompletionAsync(
                systemPrompt, userPrompt, schema, "kesinti_siniflandirma");

            var sonuc = JsonSerializer.Deserialize<ClassificationResult>(modelCikisiJson)
                ?? throw new InvalidOperationException("Model ciktisi ayristirilamadi.");

            var onerilenKategori = await kategoriRepository.FindByAdAsync(sonuc.Kategori);
            if (onerilenKategori is null)
            {
                Console.Error.WriteLine($"[HATA] KesintiKayitId={kayit.KesintiKayitId}: model kapali listede olmayan kategori dondurdu: '{sonuc.Kategori}'");
                hatali++;
                continue;
            }

            var siniflandirma = new KesintiSiniflandirma
            {
                KesintiKayitId = kayit.KesintiKayitId,
                ModelAdi = liteLlmOptions.ChatModel,
                ModelCikisiJson = modelCikisiJson,
                OnerilenKategoriId = onerilenKategori.KategoriId,
            };
            await kesintiRepository.SiniflandirmaEkleAsync(siniflandirma);

            // Standart disi dokumanlarda eksik alanlari AI'nin doldurdugu degerlerle tamamla;
            // zaten mevcut olan alanlara dokunma.
            kayit.KategoriId = onerilenKategori.KategoriId;
            kayit.Tarih ??= TryParseTarih(sonuc.Tarih);
            kayit.SureDakika ??= sonuc.SureDakika;
            kayit.EtkilenenSistemler = string.IsNullOrWhiteSpace(kayit.EtkilenenSistemler) ? sonuc.EtkilenenSistemler : kayit.EtkilenenSistemler;
            kayit.Neden = string.IsNullOrWhiteSpace(kayit.Neden) ? sonuc.Neden : kayit.Neden;
            kayit.AlinanOnlemler = string.IsNullOrWhiteSpace(kayit.AlinanOnlemler) ? sonuc.AlinanOnlemler : kayit.AlinanOnlemler;
            kayit.SiniflandirmaDurumu = SiniflandirmaDurumu.Siniflandirildi;

            await kesintiRepository.KayitGuncelleAsync(kayit);

            Console.WriteLine($"[OK] KesintiKayitId={kayit.KesintiKayitId} -> {onerilenKategori.Ad}");
            basarili++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HATA] KesintiKayitId={kayit.KesintiKayitId}: {ex.Message}");
            hatali++;
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Basarili: {basarili}, Hatali: {hatali}");
    return hatali > 0 ? 1 : 0;
}

static async Task<int> ElleDuzeltAsync(string[] args, SqlConnectionFactory connectionFactory, KategoriRepository kategoriRepository)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Kullanim: duzelt <kesintiSiniflandirmaId> <yeniKategoriAdi> <kullaniciAdi>");
        return 1;
    }

    if (!int.TryParse(args[1], out var kesintiSiniflandirmaId))
    {
        Console.Error.WriteLine("kesintiSiniflandirmaId sayisal olmalidir.");
        return 1;
    }

    var yeniKategoriAdi = args[2];
    var kullaniciAdi = args[3];

    var yeniKategori = await kategoriRepository.FindByAdAsync(yeniKategoriAdi);
    if (yeniKategori is null)
    {
        Console.Error.WriteLine($"'{yeniKategoriAdi}' adinda aktif bir kategori bulunamadi.");
        return 1;
    }

    using var connection = connectionFactory.Create();
    var etkilenenSatir = await connection.ExecuteAsync(
        """
        UPDATE dbo.KesintiSiniflandirma
        SET ElleDuzeltilmisKategoriId = @kategoriId, ElleDuzeltenKullanici = @kullaniciAdi
        WHERE KesintiSiniflandirmaId = @id
        """,
        new { kategoriId = yeniKategori.KategoriId, kullaniciAdi, id = kesintiSiniflandirmaId });

    if (etkilenenSatir == 0)
    {
        Console.Error.WriteLine($"KesintiSiniflandirmaId={kesintiSiniflandirmaId} bulunamadi.");
        return 1;
    }

    var kayitSatir = await connection.ExecuteAsync(
        """
        UPDATE k
        SET k.KategoriId = @kategoriId
        FROM dbo.KesintiKayit k
        INNER JOIN dbo.KesintiSiniflandirma s ON s.KesintiKayitId = k.KesintiKayitId
        WHERE s.KesintiSiniflandirmaId = @id
        """,
        new { kategoriId = yeniKategori.KategoriId, id = kesintiSiniflandirmaId });

    Console.WriteLine($"KesintiSiniflandirmaId={kesintiSiniflandirmaId} kategori '{yeniKategoriAdi}' olarak elle duzeltildi ({kullaniciAdi}). Etkilenen kayit: {kayitSatir}.");
    return 0;
}

static DateTime? TryParseTarih(string? deger) =>
    !string.IsNullOrWhiteSpace(deger) && DateTime.TryParse(deger, out var parsed) ? parsed : null;
