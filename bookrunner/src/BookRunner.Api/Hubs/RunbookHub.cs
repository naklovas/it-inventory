using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookRunner.Api.Hubs;

/// <summary>
/// Runbook ekranini acik tutan kullanicilara canli guncelleme gonderir:
/// gorev durumu degistiginde, atama yapildiginda veya yorum eklendiginde
/// sayfayi yenilemeden herkes ayni tabloyu gorur.
/// </summary>
[Authorize]
public sealed class RunbookHub(ILogger<RunbookHub> logger) : Hub
{
    /// <summary>Belirli bir runbook'un canli yayin grubuna katilir.</summary>
    public async Task JoinRunbook(Guid runbookId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(runbookId));
        logger.LogDebug("{User} {Runbook} runbook'unu izlemeye basladi.", Context.User?.Identity?.Name, runbookId);

        // Aciks ekranlarda "kimler bakiyor" gostergesi icin digerlerine haber verilir.
        await Clients.OthersInGroup(GroupName(runbookId))
            .SendAsync("ViewerJoined", Context.User?.Identity?.Name ?? "bilinmeyen");
    }

    public async Task LeaveRunbook(Guid runbookId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(runbookId));

    public static string GroupName(Guid runbookId) => $"runbook:{runbookId}";
}
