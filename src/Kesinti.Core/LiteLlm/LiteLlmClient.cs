using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kesinti.Core.Configuration;

namespace Kesinti.Core.LiteLlm;

/// <summary>
/// Kurum ici LiteLLM sunucusuna (OpenAI uyumlu API) istek atan istemci.
/// BaseUrl ve ApiKey yapilandirmadan gelir; koda gomulmez.
/// </summary>
public sealed class LiteLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LiteLlmOptions _options;

    public LiteLlmClient(LiteLlmOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    /// <summary>
    /// Verilen JSON semaya uyan bir yapilandirilmis cevap ister ve ham JSON metni dondurur.
    /// </summary>
    public async Task<string> GetStructuredCompletionAsync(
        string systemPrompt,
        string userPrompt,
        object jsonSchema,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest
        {
            Model = _options.ChatModel,
            Temperature = 0.0,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt },
            ],
            ResponseFormat = new JsonSchemaResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new JsonSchemaSpec
                {
                    Name = schemaName,
                    Strict = true,
                    Schema = jsonSchema,
                },
            },
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("LiteLLM cevabi bos geldi.");

        var content = payload.Choices.FirstOrDefault()?.Message.Content
            ?? throw new InvalidOperationException("LiteLLM cevabinda mesaj icerigi yok.");

        return content;
    }

    public async Task<float[]> GetEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        var results = await GetEmbeddingsAsync([input], cancellationToken);
        return results[0];
    }

    public async Task<float[][]> GetEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        var request = new EmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Input = inputs.ToList(),
        };

        using var response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("LiteLLM embedding cevabi bos geldi.");

        return payload.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToArray();
    }

    public static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
