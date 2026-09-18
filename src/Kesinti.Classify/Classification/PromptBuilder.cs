using System.Text;
using Kesinti.Core.Models;

namespace Kesinti.Classify.Classification;

public static class PromptBuilder
{
    public static string SystemPrompt(IReadOnlyList<string> kategoriAdlari)
    {
        var liste = string.Join(", ", kategoriAdlari);
        return $"""
            Sen bir BT kesinti raporu siniflandirma asistanisin.
            Gorevlerin:
            1) "kategori" alanini SADECE su kapali listeden sec: {liste}. Listede olmayan bir deger uretme.
            2) Eger asagida "eksik alanlar" olarak isaretlenmis alanlar varsa, bunlari SADECE "ham metin"de
               acikca gecen bilgiye dayanarak doldur. Ham metinde bilgi yoksa veya belirsizse o alani null birak;
               asla tahmin veya uydurma yapma.
            3) Zaten degeri olan alanlari degistirme, sadece eksik olanlari doldur.
            Cevabini sadece verilen JSON semaya uygun olarak ver.
            """;
    }

    public static string UserPrompt(KesintiKayit kayit, KesintiDokuman dokuman)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mevcut kesinti kaydi alanlari:");
        sb.AppendLine($"- Tarih: {FormatOrEksik(kayit.Tarih)}");
        sb.AppendLine($"- SureDakika: {FormatOrEksik(kayit.SureDakika)}");
        sb.AppendLine($"- EtkilenenSistemler: {FormatOrEksik(kayit.EtkilenenSistemler)}");
        sb.AppendLine($"- Neden: {FormatOrEksik(kayit.Neden)}");
        sb.AppendLine($"- AlinanOnlemler: {FormatOrEksik(kayit.AlinanOnlemler)}");
        sb.AppendLine();
        sb.AppendLine($"Dokuman format durumu: {dokuman.FormatDurumu}");
        sb.AppendLine();
        sb.AppendLine("Ham metin:");
        sb.AppendLine(dokuman.HamMetin);
        return sb.ToString();
    }

    private static string FormatOrEksik(object? deger) =>
        deger is null || (deger is string s && string.IsNullOrWhiteSpace(s))
            ? "(eksik)"
            : deger.ToString()!;
}
