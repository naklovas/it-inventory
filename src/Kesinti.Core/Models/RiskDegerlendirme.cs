namespace Kesinti.Core.Models;

public static class RiskSeviyesi
{
    public const string Dusuk = "Dusuk";
    public const string Orta = "Orta";
    public const string Yuksek = "Yuksek";
    public const string Belirsiz = "Belirsiz";
}

public sealed class RiskDegerlendirme
{
    public int RiskDegerlendirmeId { get; set; }
    public int DegisiklikKaydiId { get; set; }
    public string RiskSeviyesi { get; set; } = Models.RiskSeviyesi.Belirsiz;
    public string? DayanilanKesintiIdleriJson { get; set; }
    public string? OnerilenOnlemler { get; set; }
    public string ModelAdi { get; set; } = string.Empty;
    public string ModelCikisiJson { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
}
