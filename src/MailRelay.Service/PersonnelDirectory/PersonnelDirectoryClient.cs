using System.Collections.Concurrent;
using System.Net.Http.Json;
using MailRelay.Service.Models;
using MailRelay.Service.Options;
using Microsoft.Extensions.Options;

namespace MailRelay.Service.PersonnelDirectory;

// "doku" personel dizini servisine (http://doku:5406) erisim. PersonnelDirectory:Enabled
// false ise hicbir HTTP cagrisi yapilmaz. Kisa sureli (10 dk) sonuc onbellegi tutulur ki
// ayni kullanici adina siklikla mail gonderilirken her seferinde disari cagri yapilmasin.
public sealed class PersonnelDirectoryClient : IPersonnelDirectoryClient
{
    private static readonly TimeSpan LookupCacheDuration = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly PersonnelDirectoryOptions _options;
    private readonly ILogger<PersonnelDirectoryClient> _logger;
    private readonly ConcurrentDictionary<string, (PersonnelInfo? Info, DateTime CachedAtUtc)> _lookupCache = new(StringComparer.OrdinalIgnoreCase);

    public PersonnelDirectoryClient(HttpClient httpClient, IOptions<PersonnelDirectoryOptions> options, ILogger<PersonnelDirectoryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PersonnelInfo?> LookupAsync(string username, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(username))
            return null;

        if (_lookupCache.TryGetValue(username, out var cached) && DateTime.UtcNow - cached.CachedAtUtc < LookupCacheDuration)
            return cached.Info;

        try
        {
            var path = string.Format(_options.LookupPathTemplate, Uri.EscapeDataString(username));
            using var response = await _httpClient.GetAsync(path, ct);

            PersonnelInfo? info = null;
            if (response.IsSuccessStatusCode)
                info = await response.Content.ReadFromJsonAsync<PersonnelInfo>(cancellationToken: ct);

            _lookupCache[username] = (info, DateTime.UtcNow);
            return info;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "PersonnelDirectory lookup basarisiz: {Username}", username);
            return null;
        }
    }

    public async Task<IReadOnlyList<TeamInfo>> FetchTeamsAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
            return Array.Empty<TeamInfo>();

        var teams = await _httpClient.GetFromJsonAsync<List<TeamInfo>>(_options.TeamsPath, ct);
        return teams ?? new List<TeamInfo>();
    }
}
