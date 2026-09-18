namespace Kesinti.Core.Configuration;

/// <summary>
/// Kurum ici LiteLLM (OpenAI uyumlu) API ayarlari.
/// BaseUrl ve ApiKey her zaman appsettings.json veya ortam degiskenlerinden okunur, koda yazilmaz.
/// </summary>
public sealed class LiteLlmOptions
{
    public const string SectionName = "LiteLlm";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
}
