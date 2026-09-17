namespace Kesinti.Core.Models;

public static class FormatDurumu
{
    public const string Standart = "Standart";
    public const string StandartDisi = "Standart disi";
}

public sealed class KesintiDokuman
{
    public int KesintiDokumanId { get; set; }
    public string DosyaYolu { get; set; } = string.Empty;
    public string DosyaHash { get; set; } = string.Empty;
    public string HamMetin { get; set; } = string.Empty;
    public string FormatDurumu { get; set; } = Models.FormatDurumu.StandartDisi;
    public DateTime IslenmeZamani { get; set; }
}
