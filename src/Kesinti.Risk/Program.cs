using System.Text.Json;
using Kesinti.Core.Configuration;
using Kesinti.Core.Data;
using Kesinti.Core.LiteLlm;
using Kesinti.Core.Models;
using Kesinti.Risk.Risk;

const int EnFazlaAday = 5;
const double MinimumBenzerlikEmbedding = 0.55;
const double MinimumBenzerlikMetin = 0.12; // Jaccard skorlari embedding cosine benzerliginden dogal olarak daha dusuk cikar.

var configuration = AppConfig.Load(AppContext.BaseDirectory, args);
var dbOptions = AppConfig.GetDatabase(configuration);
var liteLlmOptions = AppConfig.GetLiteLlm(configuration);

var embeddingKullaniliyorMu = !string.IsNullOrWhiteSpace(liteLlmOptions.EmbeddingModel);
var minimumBenzerlik = embeddingKullaniliyorMu ? MinimumBenzerlikEmbedding : MinimumBenzerlikMetin;

Console.WriteLine(embeddingKullaniliyorMu
    ? $"Benzerlik yontemi: embedding ({liteLlmOptions.EmbeddingModel})"
    : "Benzerlik yontemi: kelime-bazli (embedding modeli tanimli degil - LiteLlm:EmbeddingModel bos)");

var dosyaYolu = args.FirstOrDefault(a => !a.StartsWith("--"))
    ?? configuration["Risk:DegisiklikListesiDosyasi"]
    ?? throw new InvalidOperationException(
        "Degisiklik listesi dosyasi belirtilmedi. Komut satirinda dosya yolu verin (CSV/XLSX) veya 'Risk:DegisiklikListesiDosyasi' ayarini tanimlayin.");

if (!File.Exists(dosyaYolu))
{
    Console.Error.WriteLine($"Dosya bulunamadi: {dosyaYolu}");
    return 1;
}

var connectionFactory = new SqlConnectionFactory(dbOptions);
var kesintiRepository = new KesintiRepository(connectionFactory);
var degisiklikRepository = new DegisiklikRepository(connectionFactory);
var riskRepository = new RiskRepository(connectionFactory);
var liteLlmClient = new LiteLlmClient(liteLlmOptions);

var degisiklikler = ChangeListReader.Read(dosyaYolu);
Console.WriteLine($"{degisiklikler.Count} degisiklik kaydi okundu: {dosyaYolu}");

var hatali = 0;

foreach (var degisiklik in degisiklikler)
{
    try
    {
        var degisiklikKaydiId = await degisiklikRepository.EkleAsync(degisiklik);
        degisiklik.DegisiklikKaydiId = degisiklikKaydiId;

        var ayniSistemKayitlari = await kesintiRepository.GetSistemeGoreSiniflandirilmisKayitlarAsync(degisiklik.Sistem);

        if (ayniSistemKayitlari.Count == 0)
        {
            await riskRepository.EkleAsync(new RiskDegerlendirme
            {
                DegisiklikKaydiId = degisiklikKaydiId,
                RiskSeviyesi = RiskSeviyesi.Belirsiz,
                DayanilanKesintiIdleriJson = "[]",
                OnerilenOnlemler = null,
                ModelAdi = "yok (gecmis kayit bulunamadi)",
                ModelCikisiJson = "{}",
            });

            Console.WriteLine($"[BELIRSIZ] {degisiklik.Sistem} / {degisiklik.DegisiklikTipi}: ayni sistemde gecmis kesinti kaydi yok, LLM cagrilmadi.");
            continue;
        }

        var adaylar = await CandidateSelector.SecVeSiralaAsync(
            liteLlmClient, liteLlmOptions, degisiklik, ayniSistemKayitlari, EnFazlaAday, minimumBenzerlik);

        if (adaylar.Count == 0)
        {
            await riskRepository.EkleAsync(new RiskDegerlendirme
            {
                DegisiklikKaydiId = degisiklikKaydiId,
                RiskSeviyesi = RiskSeviyesi.Belirsiz,
                DayanilanKesintiIdleriJson = "[]",
                OnerilenOnlemler = null,
                ModelAdi = "yok (yeterince benzer kayit yok)",
                ModelCikisiJson = "{}",
            });

            Console.WriteLine($"[BELIRSIZ] {degisiklik.Sistem} / {degisiklik.DegisiklikTipi}: benzerlik esigini gecen kayit yok, LLM cagrilmadi.");
            continue;
        }

        var gecerliIdler = adaylar.Select(a => a.Kayit.KesintiKayitId).ToList();
        var schema = RiskAssessmentSchema.Build(gecerliIdler);
        var userPrompt = RiskPromptBuilder.UserPrompt(degisiklik, adaylar);

        var modelCikisiJson = await liteLlmClient.GetStructuredCompletionAsync(
            RiskPromptBuilder.SystemPrompt, userPrompt, schema, "risk_degerlendirme");

        var sonuc = JsonSerializer.Deserialize<RiskAssessmentResult>(modelCikisiJson)
            ?? throw new InvalidOperationException("Model ciktisi ayristirilamadi.");

        // Guvenlik: model aday listesinde olmayan bir ID donduremesin.
        var gecerliIdSet = gecerliIdler.ToHashSet();
        var dogrulanmisIdler = sonuc.DayanilanKesintiIdleri.Where(gecerliIdSet.Contains).ToList();

        await riskRepository.EkleAsync(new RiskDegerlendirme
        {
            DegisiklikKaydiId = degisiklikKaydiId,
            RiskSeviyesi = sonuc.RiskSeviyesi,
            DayanilanKesintiIdleriJson = JsonSerializer.Serialize(dogrulanmisIdler),
            OnerilenOnlemler = sonuc.OnerilenOnlemler,
            ModelAdi = liteLlmOptions.ChatModel,
            ModelCikisiJson = modelCikisiJson,
        });

        Console.WriteLine($"[OK] {degisiklik.Sistem} / {degisiklik.DegisiklikTipi} -> Risk={sonuc.RiskSeviyesi}, dayanak sayisi={dogrulanmisIdler.Count}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[HATA] {degisiklik.Sistem} / {degisiklik.DegisiklikTipi}: {ex.Message}");
        hatali++;
    }
}

Console.WriteLine();
Console.WriteLine($"Toplam: {degisiklikler.Count}, Hatali: {hatali}");

return hatali > 0 ? 1 : 0;
