namespace Kesinti.Core.Models;

public sealed class Kategori
{
    public int KategoriId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; } = true;
}
