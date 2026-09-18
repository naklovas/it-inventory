using Kesinti.Core.Configuration;
using Kesinti.Core.LiteLlm;
using Kesinti.Core.Models;
using Kesinti.Core.Util;

namespace Kesinti.Risk.Risk;

public sealed record RankedCandidate(KesintiKayit Kayit, double Benzerlik);

/// <summary>
/// Ayni sistem filtresinden gecen gecmis kesintileri, degisiklik aciklamasiyla benzerligine gore
/// siralar. "Benzer degisiklik tipi" siniri metin benzerligiyle saglanir: DegisiklikTipi, aday
/// metnin (Neden + AlinanOnlemler) icine benzerlik karsilastirmasina dahil edilir.
///
/// LiteLlm:EmbeddingModel tanimliysa embedding cosine similarity kullanilir; kurumsal LiteLLM
/// henuz embedding sunmuyorsa (EmbeddingModel bos) otomatik olarak kelime-bazli (Jaccard)
/// benzerlige duser. Embedding modeli eklendiginde tek yapilmasi gereken appsettings'e
/// EmbeddingModel degerini yazmak; kod degismeden embedding'e gecer.
/// </summary>
public static class CandidateSelector
{
    public static async Task<List<RankedCandidate>> SecVeSiralaAsync(
        LiteLlmClient liteLlmClient,
        LiteLlmOptions liteLlmOptions,
        DegisiklikKaydi degisiklik,
        IReadOnlyList<KesintiKayit> ayniSistemKayitlari,
        int enFazlaAday,
        double minimumBenzerlik,
        CancellationToken cancellationToken = default)
    {
        if (ayniSistemKayitlari.Count == 0)
        {
            return [];
        }

        var degisiklikMetni = $"{degisiklik.DegisiklikTipi}. {degisiklik.Aciklama}".Trim();
        var adayMetinleri = ayniSistemKayitlari
            .Select(k => $"{k.EtkilenenSistemler}. Neden: {k.Neden}. Alinan onlemler: {k.AlinanOnlemler}")
            .ToList();

        var benzerlikler = string.IsNullOrWhiteSpace(liteLlmOptions.EmbeddingModel)
            ? adayMetinleri.Select(m => TextSimilarity.JaccardBenzerligi(degisiklikMetni, m)).ToArray()
            : await EmbeddingBenzerlikleriHesaplaAsync(liteLlmClient, degisiklikMetni, adayMetinleri, cancellationToken);

        var siralanan = new List<RankedCandidate>();
        for (var i = 0; i < ayniSistemKayitlari.Count; i++)
        {
            if (benzerlikler[i] >= minimumBenzerlik)
            {
                siralanan.Add(new RankedCandidate(ayniSistemKayitlari[i], benzerlikler[i]));
            }
        }

        return siralanan
            .OrderByDescending(c => c.Benzerlik)
            .Take(enFazlaAday)
            .ToList();
    }

    private static async Task<double[]> EmbeddingBenzerlikleriHesaplaAsync(
        LiteLlmClient liteLlmClient,
        string degisiklikMetni,
        List<string> adayMetinleri,
        CancellationToken cancellationToken)
    {
        var tumMetinler = new List<string> { degisiklikMetni };
        tumMetinler.AddRange(adayMetinleri);

        var embeddingler = await liteLlmClient.GetEmbeddingsAsync(tumMetinler, cancellationToken);
        var degisiklikEmbedding = embeddingler[0];

        return adayMetinleri
            .Select((_, i) => EmbeddingSimilarity.CosineSimilarity(degisiklikEmbedding, embeddingler[i + 1]))
            .ToArray();
    }
}
