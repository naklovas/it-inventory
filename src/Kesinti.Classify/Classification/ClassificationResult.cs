using System.Text.Json.Serialization;

namespace Kesinti.Classify.Classification;

public sealed class ClassificationResult
{
    [JsonPropertyName("kategori")]
    public string Kategori { get; set; } = string.Empty;

    [JsonPropertyName("tarih")]
    public string? Tarih { get; set; }

    [JsonPropertyName("sureDakika")]
    public int? SureDakika { get; set; }

    [JsonPropertyName("etkilenenSistemler")]
    public string? EtkilenenSistemler { get; set; }

    [JsonPropertyName("neden")]
    public string? Neden { get; set; }

    [JsonPropertyName("alinanOnlemler")]
    public string? AlinanOnlemler { get; set; }

    [JsonPropertyName("gerekce")]
    public string Gerekce { get; set; } = string.Empty;
}
