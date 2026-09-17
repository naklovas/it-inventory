namespace Kesinti.Ingest.Docx;

public sealed class ExtractedFields
{
    public DateTime? Tarih { get; set; }
    public int? SureDakika { get; set; }
    public string? EtkilenenSistemler { get; set; }
    public string? Neden { get; set; }
    public string? AlinanOnlemler { get; set; }

    /// <summary>
    /// Standart format icin zorunlu kabul edilen alanlarin (Tarih, Etkilenen Sistemler, Neden)
    /// tumu bulunabildiyse true.
    /// </summary>
    public bool StandardaUyuyorMu =>
        Tarih.HasValue &&
        !string.IsNullOrWhiteSpace(EtkilenenSistemler) &&
        !string.IsNullOrWhiteSpace(Neden);
}
