using System.Net.Http.Json;
using System.Text.Json;
using BookRunner.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Personnel;

/// <summary>
/// Personel servisine "GET {BaseUrl}{LookupPathTemplate}" ile sorar ve donen
/// takim adi + fotografi (base64 -> bayt dizisi) uygulamaya tasir. Servis
/// erisilemezse hata firlatmaz; cagiran taraf null alip varsayilan role/foto
/// olmadan devam eder.
/// </summary>
public sealed class PersonnelDirectoryService(
    HttpClient httpClient,
    IOptions<PersonnelDirectoryOptions> options,
    ILogger<PersonnelDirectoryService> logger) : IPersonnelDirectoryService
{
    private readonly PersonnelDirectoryOptions _options = options.Value;

    public async Task<PersonnelProfile?> GetProfileAsync(string username, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        try
        {
            var path = string.Format(_options.LookupPathTemplate, Uri.EscapeDataString(username));
            var response = await httpClient.GetFromJsonAsync<PersonnelResponse>(path, ct);
            if (response is null)
            {
                return null;
            }

            byte[]? thumbnail = null;
            if (!string.IsNullOrWhiteSpace(response.Thumbnail))
            {
                try
                {
                    thumbnail = Convert.FromBase64String(response.Thumbnail);
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(ex, "{User} icin personel servisi gecersiz thumbnail dondurdu.", username);
                }
            }

            return new PersonnelProfile(response.Username ?? username, response.TeamName, thumbnail);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "{User} icin personel servisine erisilemedi.", username);
            return null;
        }
    }

    private sealed class PersonnelResponse
    {
        public string? Username { get; set; }
        public string? TeamName { get; set; }
        public string? Thumbnail { get; set; }
    }
}

/// <summary>Servis kapaliyken (Enabled: false veya BaseUrl bos) kullanilan bos uygulama.</summary>
public sealed class NullPersonnelDirectoryService : IPersonnelDirectoryService
{
    public Task<PersonnelProfile?> GetProfileAsync(string username, CancellationToken ct = default)
        => Task.FromResult<PersonnelProfile?>(null);
}
