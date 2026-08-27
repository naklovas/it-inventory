using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Security;

/// <summary>
/// <see cref="IRunbookAccess"/> uygulamasi.
///
/// Once rol izni kontrol edilir (veritabanina gitmeden); yalnizca izin yoksa
/// sahiplik sorgusu calisir. Yonetici rolu tum izinlere sahip oldugu icin
/// ayrica ozel bir durum yazmaya gerek yoktur.
/// </summary>
public sealed class RunbookAccess(IAppDbContext db, ICurrentUser currentUser) : IRunbookAccess
{
    public void Ensure(string permission)
    {
        if (!HasPermission(permission))
        {
            throw new ForbiddenException($"Bu islem icin '{permission}' yetkisi gerekiyor.");
        }
    }

    public async Task EnsureForRunbookAsync(Guid runbookId, string permission, CancellationToken ct = default)
    {
        if (HasPermission(permission) || await IsOwnerOfRunbookAsync(runbookId, ct))
        {
            return;
        }

        throw Forbidden(permission);
    }

    public async Task EnsureForTaskAsync(Guid taskId, string permission, CancellationToken ct = default)
    {
        if (HasPermission(permission) || await IsOwnerOfTaskAsync(taskId, ct))
        {
            return;
        }

        throw Forbidden(permission);
    }

    public async Task<bool> IsOwnerOfRunbookAsync(Guid runbookId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return false;
        }

        return await db.Runbooks.AnyAsync(r => r.Id == runbookId && r.OwnerUserId == userId.Value, ct);
    }

    public async Task<bool> IsOwnerOfTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return false;
        }

        return await db.Tasks.AnyAsync(t => t.Id == taskId && t.Runbook.OwnerUserId == userId.Value, ct);
    }

    private bool HasPermission(string permission) => Permissions.Has(currentUser.Role, permission);

    private static ForbiddenException Forbidden(string permission)
        => new($"Bu islem icin '{permission}' yetkisi veya runbook sahipligi gerekiyor.");
}
