namespace BookRunner.Infrastructure.Integration;

/// <summary>
/// Ucuncu parti REST API entegrasyonu ayarlari (appsettings: "Integration").
/// Sohbet kanali webhook'u, ITSM bildirimi veya kurumsal olay veri yolu icin kullanilir.
/// </summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    public bool Enabled { get; set; }

    /// <summary>Olaylarin POST edilecegi adres.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>BaseUrl'e eklenen yol, orn. "/api/events".</summary>
    public string EventPath { get; set; } = "/api/events";

    /// <summary>Varsa API anahtari; <see cref="ApiKeyHeader"/> basliginda gonderilir.</summary>
    public string? ApiKey { get; set; }

    public string ApiKeyHeader { get; set; } = "X-Api-Key";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Gecici hatalarda tekrar deneme sayisi.</summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>Hangi olaylarin gonderilecegi. Bos ise tum olaylar gonderilir.</summary>
    public string[] EventTypes { get; set; } = [];
}
