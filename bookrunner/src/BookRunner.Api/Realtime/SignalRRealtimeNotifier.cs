using BookRunner.Api.Hubs;
using BookRunner.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace BookRunner.Api.Realtime;

/// <summary>Is katmanindaki olaylari SignalR uzerinden acik ekranlara iletir.</summary>
public sealed class SignalRRealtimeNotifier(IHubContext<RunbookHub> hub) : IRealtimeNotifier
{
    public Task TaskChangedAsync(Guid runbookId, Guid taskId, string changeType, CancellationToken ct = default)
        => hub.Clients.Group(RunbookHub.GroupName(runbookId))
            .SendAsync("TaskChanged", new { runbookId, taskId, changeType }, ct);

    public Task CommentAddedAsync(Guid runbookId, Guid taskId, object comment, CancellationToken ct = default)
        => hub.Clients.Group(RunbookHub.GroupName(runbookId))
            .SendAsync("CommentAdded", new { runbookId, taskId, comment }, ct);

    public Task RunbookChangedAsync(Guid runbookId, string changeType, CancellationToken ct = default)
        => hub.Clients.Group(RunbookHub.GroupName(runbookId))
            .SendAsync("RunbookChanged", new { runbookId, changeType }, ct);
}
