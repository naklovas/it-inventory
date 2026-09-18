using System.Text.RegularExpressions;

namespace Kesinti.Core.Util;

/// <summary>
/// Embedding modeli tanimli olmadigi durumlarda kullanilan basit kelime-bazli benzerlik olcumu
/// (Jaccard). Embedding modeli eklendiginde ayni islevi cosine similarity ile degistirmek yeterlidir.
/// </summary>
public static class TextSimilarity
{
    public static double JaccardBenzerligi(string a, string b)
    {
        var kelimelerA = Tokenize(a);
        var kelimelerB = Tokenize(b);

        if (kelimelerA.Count == 0 || kelimelerB.Count == 0)
        {
            return 0;
        }

        var kesisim = kelimelerA.Intersect(kelimelerB).Count();
        var birlesim = kelimelerA.Union(kelimelerB).Count();
        return birlesim == 0 ? 0 : (double)kesisim / birlesim;
    }

    // Turkce eklemeli (agglutinative) bir dil oldugu icin "veritabani" / "veritabanindaki" gibi
    // ekli formlar tam eslesmez. Kaba bir govde yaklasimi olarak uzun kelimeler ilk 5 karaktere
    // indirilir; boylece ortak govdeye sahip kelimeler eslesir.
    private const int GovdeUzunlugu = 5;

    private static HashSet<string> Tokenize(string metin) =>
        Regex.Matches(metin.ToLowerInvariant(), @"[a-zçğıöşü0-9]+")
            .Select(m => m.Value)
            .Where(w => w.Length > 2)
            .Select(w => w.Length > GovdeUzunlugu ? w[..GovdeUzunlugu] : w)
            .ToHashSet();
}
