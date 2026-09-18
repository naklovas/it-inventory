using System.Globalization;
using System.Text.RegularExpressions;

namespace Kesinti.Ingest.Docx;

/// <summary>
/// Standart kesinti raporu formatindaki "Etiket: deger" satirlarini kural tabanli olarak ayiklar.
/// Beklenen etiketler: Tarih, Sure, Etkilenen Sistem(ler), Neden, Alinan Onlem(ler).
/// Bir alan bulunamazsa veya ayristirilamazsa bos birakilir; cagiran taraf
/// ExtractedFields.StandardaUyuyorMu = false oldugunda FormatDurumu = 'Standart disi' isaretler.
/// </summary>
public static class FieldExtractor
{
    private static readonly (string Alan, Regex Etiket)[] EtiketPatterns =
    [
        ("Tarih", new Regex(@"^\s*(kesinti\s+)?tarih[i]?\s*[:\-]\s*(?<deger>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("Sure", new Regex(@"^\s*(kesinti\s+)?s[üu]re(si)?\s*[:\-]\s*(?<deger>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("EtkilenenSistemler", new Regex(@"^\s*etkilenen\s+sistem(ler)?\s*[:\-]\s*(?<deger>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("Neden", new Regex(@"^\s*(kesinti\s+)?neden[i]?\s*[:\-]\s*(?<deger>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("AlinanOnlemler", new Regex(@"^\s*al[ıi]nan\s+[öo]nlem(ler)?\s*[:\-]\s*(?<deger>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
    ];

    public static ExtractedFields Extract(string hamMetin)
    {
        var lines = hamMetin.Replace("\r\n", "\n").Split('\n');
        var degerler = new Dictionary<string, string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var (alan, etiket) in EtiketPatterns)
            {
                var match = etiket.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var deger = match.Groups["deger"].Value.Trim();

                if (deger.Length == 0)
                {
                    deger = CaptureFollowingLines(lines, i + 1);
                }

                if (deger.Length > 0)
                {
                    degerler[alan] = deger;
                }

                break;
            }
        }

        var fields = new ExtractedFields
        {
            EtkilenenSistemler = degerler.GetValueOrDefault("EtkilenenSistemler"),
            Neden = degerler.GetValueOrDefault("Neden"),
            AlinanOnlemler = degerler.GetValueOrDefault("AlinanOnlemler"),
        };

        if (degerler.TryGetValue("Tarih", out var tarihMetni))
        {
            fields.Tarih = ParseTarih(tarihMetni);
        }

        if (degerler.TryGetValue("Sure", out var sureMetni))
        {
            fields.SureDakika = ParseSureDakika(sureMetni);
        }

        return fields;
    }

    private static string CaptureFollowingLines(string[] lines, int startIndex)
    {
        var toplanan = new List<string>();
        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                break;
            }

            if (EtiketPatterns.Any(p => p.Etiket.IsMatch(line)))
            {
                break;
            }

            toplanan.Add(line);
        }

        return string.Join(" ", toplanan).Trim();
    }

    private static DateTime? ParseTarih(string deger)
    {
        var formats = new[]
        {
            "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
            "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
        };

        var ilkKisim = deger.Split(' ', 2)[0];
        foreach (var candidate in new[] { deger, ilkKisim })
        {
            if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                return exact;
            }
        }

        if (DateTime.TryParse(deger, new CultureInfo("tr-TR"), DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? ParseSureDakika(string deger)
    {
        // "10:00 - 10:45" gibi bir zaman araligi ise farki dakika olarak hesapla.
        var aralikMatch = Regex.Match(deger, @"(?<baslangic>\d{1,2}:\d{2})\s*-\s*(?<bitis>\d{1,2}:\d{2})");
        if (aralikMatch.Success &&
            TimeSpan.TryParse(aralikMatch.Groups["baslangic"].Value, out var baslangic) &&
            TimeSpan.TryParse(aralikMatch.Groups["bitis"].Value, out var bitis))
        {
            var fark = bitis - baslangic;
            if (fark.TotalMinutes > 0)
            {
                return (int)fark.TotalMinutes;
            }
        }

        var toplamDakika = 0;
        var saatMatch = Regex.Match(deger, @"(?<saat>\d+([.,]\d+)?)\s*saat", RegexOptions.IgnoreCase);
        if (saatMatch.Success)
        {
            var saat = double.Parse(saatMatch.Groups["saat"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            toplamDakika += (int)Math.Round(saat * 60);
        }

        var dakikaMatch = Regex.Match(deger, @"(?<dakika>\d+)\s*dak", RegexOptions.IgnoreCase);
        if (dakikaMatch.Success)
        {
            toplamDakika += int.Parse(dakikaMatch.Groups["dakika"].Value, CultureInfo.InvariantCulture);
        }

        if (toplamDakika > 0)
        {
            return toplamDakika;
        }

        // Etiket ustunde ayrica birim gecmeyen tek sayi (dakika kabul edilir).
        var sadeceSayi = Regex.Match(deger, @"^\s*(?<sayi>\d+)\s*$");
        if (sadeceSayi.Success)
        {
            return int.Parse(sadeceSayi.Groups["sayi"].Value, CultureInfo.InvariantCulture);
        }

        return null;
    }
}
