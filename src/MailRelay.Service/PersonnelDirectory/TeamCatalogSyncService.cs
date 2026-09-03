using MailRelay.Service.Options;
using Microsoft.Extensions.Options;

namespace MailRelay.Service.PersonnelDirectory;

// PersonnelDirectory:TeamCatalogSyncMinutes araligiyla /api/takimlar ucundan tum ekip
// listesini ceker ve TeamCatalogStore'a yazar; admin panelindeki takim filtre listesi
// (ve ileride kullanilabilecek takim bazli raporlama) buradan beslenir.
public sealed class TeamCatalogSyncService : BackgroundService
{
    private readonly IPersonnelDirectoryClient _client;
    private readonly TeamCatalogStore _store;
    private readonly PersonnelDirectoryOptions _options;
    private readonly ILogger<TeamCatalogSyncService> _logger;

    public TeamCatalogSyncService(
        IPersonnelDirectoryClient client,
        TeamCatalogStore store,
        IOptions<PersonnelDirectoryOptions> options,
        ILogger<TeamCatalogSyncService> logger)
    {
        _client = client;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var teams = await _client.FetchTeamsAsync(stoppingToken);
                _store.Replace(teams);
                _logger.LogInformation("Takim katalogu senkronize edildi: {Count} ekip.", teams.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Takim katalogu senkronizasyonu basarisiz.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.TeamCatalogSyncMinutes)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
