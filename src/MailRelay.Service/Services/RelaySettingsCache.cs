using MailRelay.Service.Data;
using MailRelay.Service.Models;
using MailRelay.Service.Options;
using Microsoft.Extensions.Options;

namespace MailRelay.Service.Services;

// RelaySettings her gonderimde veritabanindan okunmasin diye kisa sureli (varsayilan 30sn)
// bellek onbellegi tutar. Admin panelinden ayar guncellendiginde Invalidate() cagrilir,
// boylece degisiklik en fazla bir sonraki okuma kadar gecikmeyle uygulanir.
public sealed class RelaySettingsCache
{
    private readonly RelaySettingsRepository _repository;
    private readonly QueueOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private RelaySettings? _cached;
    private DateTime _cachedAtUtc;

    public RelaySettingsCache(RelaySettingsRepository repository, IOptions<QueueOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<RelaySettings?> GetAsync(CancellationToken ct = default)
    {
        if (IsFresh())
            return _cached;

        await _lock.WaitAsync(ct);
        try
        {
            if (IsFresh())
                return _cached;

            _cached = await _repository.GetAsync(ct);
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _cached = null;

    private bool IsFresh() =>
        _cached is not null && (DateTime.UtcNow - _cachedAtUtc).TotalSeconds < _options.RelaySettingsCacheSeconds;
}
