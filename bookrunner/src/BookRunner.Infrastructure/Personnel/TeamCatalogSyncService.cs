using BookRunner.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Personnel;

/// <summary>
/// Personel servisindeki tum ekip adlarini ("/api/takimlar") periyodik olarak
/// yerel takim katalogua yansitir. Bu olmadan atama arama kutusunda yalnizca
/// daha once BookRunner'a giris yapmis/aranmis kisilerin takimlari gorunurdu;
/// bu servis sayesinde sirketteki tum ekipler bastan aranabilir olur - uyelik
/// ise kisilerin kendi girisleri/aramalari uzerinden ayrica kurulur.
/// </summary>
public sealed class TeamCatalogSyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<PersonnelDirectoryOptions> options,
    ILogger<TeamCatalogSyncService> logger) : BackgroundService
{
    private readonly PersonnelDirectoryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(_options.TeamCatalogSyncMinutes, 5));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ekip katalogu senkronu basarisiz oldu.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<IDirectorySyncService>();
        await directory.SyncTeamCatalogAsync(ct);
    }
}
