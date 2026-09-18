namespace Kesinti.Core.Models;

public static class SiniflandirmaDurumu
{
    public const string Bekliyor = "Bekliyor";
    public const string Siniflandirildi = "Siniflandirildi";
}

public sealed class KesintiKayit
{
    public int KesintiKayitId { get; set; }
    public int KesintiDokumanId { get; set; }
    public DateTime? Tarih { get; set; }
    public int? SureDakika { get; set; }
    public string? EtkilenenSistemler { get; set; }
    public string? Neden { get; set; }
    public string? AlinanOnlemler { get; set; }
    public int? KategoriId { get; set; }
    public string SiniflandirmaDurumu { get; set; } = Models.SiniflandirmaDurumu.Bekliyor;
}
