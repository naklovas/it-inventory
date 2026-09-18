using System.Text;
using Kesinti.Core.Models;

namespace Kesinti.Risk.Risk;

public static class RiskPromptBuilder
{
    public const string SystemPrompt = """
        Sen bir BT degisiklik risk analisti asistanisin.
        Sana bir planlanan degisiklik ve bu degisiklige benzer, ayni sistemde gecmiste yasanmis
        kesinti kayitlari (aday listesi) verilecek.
        Kurallar:
        1) Risk seviyesini SADECE verilen aday kayitlara dayanarak belirle (Dusuk/Orta/Yuksek).
        2) "dayanilanKesintiIdleri" alanina SADECE aday listesinde verilen KesintiKayitId degerlerini yaz;
           baska bir ID uretme.
        3) "onerilenOnlemler" alanini SADECE dayanak aldigin kayitlardaki "Alinan Onlemler" bilgisine
           dayandirarak yaz; kayitlarda olmayan bir onlem uydurma.
        """;

    public static string UserPrompt(DegisiklikKaydi degisiklik, IReadOnlyList<RankedCandidate> adaylar)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Planlanan degisiklik:");
        sb.AppendLine($"- Sistem: {degisiklik.Sistem}");
        sb.AppendLine($"- DegisiklikTipi: {degisiklik.DegisiklikTipi}");
        sb.AppendLine($"- Aciklama: {degisiklik.Aciklama}");
        sb.AppendLine($"- PlanlananTarih: {degisiklik.PlanlananTarih}");
        sb.AppendLine();
        sb.AppendLine("Ayni sistemdeki benzer gecmis kesinti kayitlari (aday listesi):");

        foreach (var aday in adaylar)
        {
            var k = aday.Kayit;
            sb.AppendLine($"- KesintiKayitId={k.KesintiKayitId} | Benzerlik={aday.Benzerlik:F3} | Tarih={k.Tarih} | " +
                          $"EtkilenenSistemler={k.EtkilenenSistemler} | Neden={k.Neden} | AlinanOnlemler={k.AlinanOnlemler}");
        }

        return sb.ToString();
    }
}
