using MailRelay.Service.Data;
using MailRelay.Service.Options;
using Microsoft.Extensions.Options;

namespace MailRelay.Service.Services;

// Kuyruk tuketicisi: N paralel worker + bir DB tarama (poll) dongusunden olusur.
//  - Worker'lar MailQueueChannel'dan id okuyup atomik TryClaimAsync ile satiri "Processing"e
//    cekmeye calisir; kaybedenler (0 satir guncellenirse) sessizce vazgecer - bu sayede ayni
//    kayit birden fazla kez gonderilmez, hatta servis birden fazla kopya calistirilsa bile.
//  - Poll dongusu; yeniden baslatmadan kurtarma ve zamanlanmis retry'lar icin Queued/Retrying
//    durumundaki kayitlari kanal'a yeniden yazar (kanal doluysa bir sonraki turda tekrar denenir).
// Worker sayisi, appsettings > Queue:WorkerCount ile RelaySettings.MaxConcurrentSend
// (veritabani, admin panelinden yonetilir) degerlerinin kucuk olani kadar baslatilir;
// MaxConcurrentSend'i degistirmek servis yeniden baslatildiginda etkin olur.
public sealed class MailQueueProcessor : BackgroundService
{
    private readonly MailQueueChannel _channel;
    private readonly MailQueueRepository _repository;
    private readonly RelaySettingsCache _settingsCache;
    private readonly ISmtpMailSender _sender;
    private readonly QueueOptions _options;
    private readonly ILogger<MailQueueProcessor> _logger;

    public MailQueueProcessor(
        MailQueueChannel channel,
        MailQueueRepository repository,
        RelaySettingsCache settingsCache,
        ISmtpMailSender sender,
        IOptions<QueueOptions> options,
        ILogger<MailQueueProcessor> logger)
    {
        _channel = channel;
        _repository = repository;
        _settingsCache = settingsCache;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialSettings = await SafeGetSettingsAsync(stoppingToken);
        var workerCount = Math.Max(1, Math.Min(_options.WorkerCount, initialSettings?.MaxConcurrentSend ?? _options.MaxConcurrentSend));
        _logger.LogInformation("Mail kuyruk islemcisi baslatiliyor: {WorkerCount} worker.", workerCount);

        var tasks = new List<Task>();
        for (var i = 0; i < workerCount; i++)
        {
            var workerIndex = i;
            tasks.Add(Task.Run(() => ConsumeLoopAsync(workerIndex, stoppingToken), stoppingToken));
        }

        tasks.Add(Task.Run(() => PollLoopAsync(stoppingToken), stoppingToken));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Normal kapanis akisi.
        }
    }

    private async Task ConsumeLoopAsync(int workerIndex, CancellationToken ct)
    {
        try
        {
            await foreach (var id in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await ProcessAsync(id, ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Worker {WorkerIndex}: mail {Id} islenirken beklenmeyen hata.", workerIndex, id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Servis durduruluyor.
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ids = await _repository.PollClaimableIdsAsync(_options.PollBatchSize, ct);
                foreach (var id in ids)
                    _channel.TryEnqueue(id);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Kuyruk taramasi sirasinda hata.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(long id, CancellationToken ct)
    {
        var item = await _repository.TryClaimAsync(id, ct);
        if (item is null)
            return; // baska bir worker/servis ornegi zaten aldi ya da artik gonderilebilir durumda degil

        var settings = await SafeGetSettingsAsync(ct);
        if (settings is null || !settings.Enabled)
        {
            await _repository.MarkRetryAsync(
                id, "Relay ayarlari tanimli degil ya da devre disi birakilmis.",
                DateTime.UtcNow.AddSeconds(_options.BaseRetryDelaySeconds), item.Attempts, ct);
            return;
        }

        try
        {
            var attachments = await _repository.GetAttachmentsAsync(id, ct);
            await _sender.SendAsync(settings, item, attachments, ct);
            await _repository.MarkSentAsync(id, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var attempts = item.Attempts + 1;
            if (attempts >= item.MaxAttempts)
            {
                await _repository.MarkFailedAsync(id, ex.Message, attempts, ct);
                _logger.LogWarning(ex, "Mail {Id} {Attempts} denemeden sonra kalici olarak basarisiz oldu.", id, attempts);
            }
            else
            {
                var delaySeconds = Math.Min(
                    _options.MaxRetryDelaySeconds,
                    _options.BaseRetryDelaySeconds * (int)Math.Pow(2, attempts - 1));
                await _repository.MarkRetryAsync(id, ex.Message, DateTime.UtcNow.AddSeconds(delaySeconds), attempts, ct);
                _logger.LogInformation(
                    "Mail {Id} gonderilemedi, {Delay}sn sonra tekrar denenecek (deneme {Attempts}/{Max}).",
                    id, delaySeconds, attempts, item.MaxAttempts);
            }
        }
    }

    private async Task<Models.RelaySettings?> SafeGetSettingsAsync(CancellationToken ct)
    {
        try
        {
            return await _settingsCache.GetAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Relay ayarlari okunamadi.");
            return null;
        }
    }
}
