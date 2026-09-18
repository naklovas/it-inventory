namespace Kesinti.Core.Models;

public sealed class DegisiklikKaydi
{
    public int DegisiklikKaydiId { get; set; }
    public string Sistem { get; set; } = string.Empty;
    public string DegisiklikTipi { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public DateTime? PlanlananTarih { get; set; }
    public string? KaynakDosya { get; set; }
}
