using Kesinti.Core.LiteLlm;
using Kesinti.Core.Models;

namespace Kesinti.Risk.Risk;

public sealed record RankedCandidate(KesintiKayit Kayit, double Benzerlik);

/// <summary>
/// Ayni sistem filtresinden gecen gecmis kesintileri, degisiklik aciklamasiyla embedding
/// benzerligine gore siralar. "Benzer degisiklik tipi" siniri metin benzerligiyle saglanir:
/// DegisiklikTipi, aday metnin (Neden + AlinanOnlemler) icine embedding karsilastirmasina dahil edilir.
/// </summary>
public static class CandidateSelector
{
    public static async Task<List<RankedCandidate>> SecVeSiralaAsync(
        LiteLlmClient liteLlmClient,
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

        var tumMetinler = new List<string> { degisiklikMetni };
        tumMetinler.AddRange(adayMetinleri);

        var embeddingler = await liteLlmClient.GetEmbeddingsAsync(tumMetinler, cancellationToken);
        var degisiklikEmbedding = embeddingler[0];

        var siralanan = new List<RankedCandidate>();
        for (var i = 0; i < ayniSistemKayitlari.Count; i++)
        {
            var benzerlik = EmbeddingSimilarity.CosineSimilarity(degisiklikEmbedding, embeddingler[i + 1]);
            if (benzerlik >= minimumBenzerlik)
            {
                siralanan.Add(new RankedCandidate(ayniSistemKayitlari[i], benzerlik));
            }
        }

        return siralanan
            .OrderByDescending(c => c.Benzerlik)
            .Take(enFazlaAday)
            .ToList();
    }
}
