using System.Text.Json.Serialization;

namespace Kesinti.Core.LiteLlm;

public sealed class EmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public List<string> Input { get; set; } = new();
}

public sealed class EmbeddingResponse
{
    [JsonPropertyName("data")]
    public List<EmbeddingData> Data { get; set; } = new();
}

public sealed class EmbeddingData
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();

    [JsonPropertyName("index")]
    public int Index { get; set; }
}
