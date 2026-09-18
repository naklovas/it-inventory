using System.Globalization;
using ClosedXML.Excel;
using Kesinti.Core.Models;

namespace Kesinti.Risk.Risk;

/// <summary>
/// Haftalik degisiklik listesini CSV veya Excel (.xlsx) dosyasindan okur.
/// Beklenen basliklar (buyuk/kucuk harf duyarsiz): Sistem, DegisiklikTipi, Aciklama, PlanlananTarih.
/// </summary>
public static class ChangeListReader
{
    private static readonly string[] Basliklar = ["Sistem", "DegisiklikTipi", "Aciklama", "PlanlananTarih"];

    public static List<DegisiklikKaydi> Read(string dosyaYolu)
    {
        var uzanti = Path.GetExtension(dosyaYolu).ToLowerInvariant();
        return uzanti switch
        {
            ".csv" => ReadCsv(dosyaYolu),
            ".xlsx" => ReadExcel(dosyaYolu),
            _ => throw new NotSupportedException($"Desteklenmeyen dosya turu: {uzanti}. Yalnizca .csv ve .xlsx desteklenir."),
        };
    }

    private static List<DegisiklikKaydi> ReadCsv(string dosyaYolu)
    {
        var satirlar = File.ReadAllLines(dosyaYolu);
        if (satirlar.Length == 0)
        {
            return [];
        }

        var basliklar = SplitCsvLine(satirlar[0]);
        var sutunIndeksleri = SutunIndeksleriBul(basliklar);

        var sonuc = new List<DegisiklikKaydi>();
        for (var i = 1; i < satirlar.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(satirlar[i]))
            {
                continue;
            }

            var hucreler = SplitCsvLine(satirlar[i]);
            sonuc.Add(SatirdanKayitOlustur(hucreler, sutunIndeksleri, dosyaYolu));
        }

        return sonuc;
    }

    private static List<DegisiklikKaydi> ReadExcel(string dosyaYolu)
    {
        using var workbook = new XLWorkbook(dosyaYolu);
        var worksheet = workbook.Worksheets.First();
        var satirlar = worksheet.RowsUsed().ToList();
        if (satirlar.Count == 0)
        {
            return [];
        }

        var baslikSatiri = satirlar[0];
        var basliklar = baslikSatiri.Cells().Select(c => c.GetString()).ToArray();
        var sutunIndeksleri = SutunIndeksleriBul(basliklar);

        var sonuc = new List<DegisiklikKaydi>();
        foreach (var satir in satirlar.Skip(1))
        {
            var hucreler = new string[basliklar.Length];
            for (var col = 0; col < basliklar.Length; col++)
            {
                hucreler[col] = satir.Cell(col + 1).GetString();
            }

            if (hucreler.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            sonuc.Add(SatirdanKayitOlustur(hucreler, sutunIndeksleri, dosyaYolu));
        }

        return sonuc;
    }

    private static Dictionary<string, int> SutunIndeksleriBul(string[] basliklar)
    {
        var indeksler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < basliklar.Length; i++)
        {
            var temiz = basliklar[i].Trim();
            if (Basliklar.Any(b => string.Equals(b, temiz, StringComparison.OrdinalIgnoreCase)))
            {
                indeksler[temiz] = i;
            }
        }

        foreach (var zorunlu in new[] { "Sistem", "DegisiklikTipi" })
        {
            if (!indeksler.ContainsKey(zorunlu))
            {
                throw new InvalidOperationException($"Degisiklik listesinde zorunlu '{zorunlu}' sutunu bulunamadi.");
            }
        }

        return indeksler;
    }

    private static DegisiklikKaydi SatirdanKayitOlustur(string[] hucreler, Dictionary<string, int> sutunIndeksleri, string dosyaYolu)
    {
        string? Al(string ad) => sutunIndeksleri.TryGetValue(ad, out var idx) && idx < hucreler.Length
            ? hucreler[idx].Trim()
            : null;

        var planlananTarih = ParseTarih(Al("PlanlananTarih"));

        return new DegisiklikKaydi
        {
            Sistem = Al("Sistem") ?? string.Empty,
            DegisiklikTipi = Al("DegisiklikTipi") ?? string.Empty,
            Aciklama = Al("Aciklama"),
            PlanlananTarih = planlananTarih,
            KaynakDosya = dosyaYolu,
        };
    }

    private static DateTime? ParseTarih(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger))
        {
            return null;
        }

        string[] formats = ["dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm"];
        if (DateTime.TryParseExact(deger, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(deger, new CultureInfo("tr-TR"), DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static string[] SplitCsvLine(string line)
    {
        // Basit CSV ayirici: tirnak icindeki virgulleri korur.
        var alanlar = new List<string>();
        var mevcut = new System.Text.StringBuilder();
        var tirnakIcinde = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                tirnakIcinde = !tirnakIcinde;
            }
            else if (c == ',' && !tirnakIcinde)
            {
                alanlar.Add(mevcut.ToString());
                mevcut.Clear();
            }
            else
            {
                mevcut.Append(c);
            }
        }

        alanlar.Add(mevcut.ToString());
        return alanlar.ToArray();
    }
}
