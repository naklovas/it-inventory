using BookRunner.Application.Abstractions;

namespace BookRunner.Infrastructure.Realtime;

/// <summary>
/// SignalR'in bulunmadigi calisma ortamlari (arka plan islemleri, konsol araclari)
/// icin bos bildirici. API projesi kendi SignalR uygulamasini kaydeder.
/// </summary>
public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task TaskChangedAsync(Guid runbookId, Guid taskId, string changeType, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CommentAddedAsync(Guid runbookId, Guid taskId, object comment, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RunbookChangedAsync(Guid runbookId, string changeType, CancellationToken ct = default)
        => Task.CompletedTask;
}
