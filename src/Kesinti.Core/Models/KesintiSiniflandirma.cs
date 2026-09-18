namespace Kesinti.Core.Models;

public sealed class KesintiSiniflandirma
{
    public int KesintiSiniflandirmaId { get; set; }
    public int KesintiKayitId { get; set; }
    public string ModelAdi { get; set; } = string.Empty;
    public string ModelCikisiJson { get; set; } = string.Empty;
    public int? OnerilenKategoriId { get; set; }
    public int? ElleDuzeltilmisKategoriId { get; set; }
    public string? ElleDuzeltenKullanici { get; set; }
    public DateTime Tarih { get; set; }
}
