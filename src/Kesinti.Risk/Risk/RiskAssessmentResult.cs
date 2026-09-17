using System.Text.Json.Serialization;

namespace Kesinti.Risk.Risk;

public sealed class RiskAssessmentResult
{
    [JsonPropertyName("riskSeviyesi")]
    public string RiskSeviyesi { get; set; } = string.Empty;

    [JsonPropertyName("dayanilanKesintiIdleri")]
    public List<int> DayanilanKesintiIdleri { get; set; } = new();

    [JsonPropertyName("onerilenOnlemler")]
    public string OnerilenOnlemler { get; set; } = string.Empty;
}
