using System.Net.Http.Json;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Integration;

/// <summary>
/// Runbook olaylarini disaridaki bir REST API'ye (sohbet kanali, ITSM, olay veri
/// yolu) iletir. Entegrasyon hatalari kullanici islemini bozmaz; yalnizca loglanir.
/// </summary>
public sealed class ExternalIntegrationClient(
    HttpClient httpClient,
    IOptions<IntegrationOptions> options,
    ILogger<ExternalIntegrationClient> logger) : IExternalIntegrationClient
{
    private readonly IntegrationOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<bool> PublishEventAsync(ExternalEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (_options.EventTypes.Length > 0 &&
            !_options.EventTypes.Contains(payload.EventType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var attempts = Math.Max(1, _options.RetryCount + 1);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync(_options.EventPath, payload, ct);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogDebug("{EventType} olayi entegrasyon hedefine iletildi.", payload.EventType);
                    return true;
                }

                logger.LogWarning("Entegrasyon hedefi {Status} dondu ({Attempt}/{Attempts}).",
                    (int)response.StatusCode, attempt, attempts);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "Entegrasyon istegi basarisiz ({Attempt}/{Attempts}).", attempt, attempts);
            }

            if (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        return false;
    }
}

/// <summary>Entegrasyon kapaliyken kullanilan bos uygulama.</summary>
public sealed class NullIntegrationClient : IExternalIntegrationClient
{
    public bool IsEnabled => false;

    public Task<bool> PublishEventAsync(ExternalEvent payload, CancellationToken ct = default)
        => Task.FromResult(false);
}
