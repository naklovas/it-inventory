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
///
/// Bunlara ek olarak, runbook sahibinin bu runbook'a ozel olarak (global role
/// dokunmadan) atadigi "Editor"lar da belirli izinler (gorev yazma/atama/yorum)
/// icin gecerlidir - bkz. <see cref="CollaboratorPermissions"/>.
/// </summary>
public sealed class RunbookAccess(IAppDbContext db, ICurrentUser currentUser) : IRunbookAccess
{
    /// <summary>Bir "Editor"un runbook sahipligi olmadan yapabildigi tek izinler.</summary>
    private static readonly string[] CollaboratorPermissions =
        [Permissions.TaskWrite, Permissions.TaskAssign, Permissions.TaskComment];

    public void Ensure(string permission)
    {
        if (!HasPermission(permission))
        {
            throw new ForbiddenException($"Bu islem icin '{permission}' yetkisi gerekiyor.");
        }
    }

    public async Task EnsureForRunbookAsync(Guid runbookId, string permission, CancellationToken ct = default)
    {
        if (HasPermission(permission) ||
            await IsOwnerOfRunbookAsync(runbookId, ct) ||
            await IsCollaboratorAsync(runbookId, permission, ct))
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

        if (CollaboratorPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            var runbookId = await db.Tasks
                .Where(t => t.Id == taskId)
                .Select(t => (Guid?)t.RunbookId)
                .FirstOrDefaultAsync(ct);

            if (runbookId is not null && await IsCollaboratorAsync(runbookId.Value, permission, ct))
            {
                return;
            }
        }

        throw Forbidden(permission);
    }

    public async Task<bool> IsOwnerOfRunbookAsync(Guid runbookId, CancellationToken ct = default)
    {
        // Test modunda (bkz. ICurrentUser.IsImpersonating) sahiplik hic sayilmaz;
        // aksi halde bir yonetici kendi actigi runbook'larda sahiplik yoluyla
        // her zaman tam yetkili kalir ve rol testi anlamsizlasir.
        var userId = currentUser.UserId;
        if (userId is null || currentUser.IsImpersonating)
        {
            return false;
        }

        return await db.Runbooks.AnyAsync(r => r.Id == runbookId && r.OwnerUserId == userId.Value, ct);
    }

    public async Task<bool> IsOwnerOfTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null || currentUser.IsImpersonating)
        {
            return false;
        }

        return await db.Tasks.AnyAsync(t => t.Id == taskId && t.Runbook.OwnerUserId == userId.Value, ct);
    }

    public async Task EnsureOwnerAsync(Guid runbookId, CancellationToken ct = default)
    {
        if (await IsOwnerOfRunbookAsync(runbookId, ct))
        {
            return;
        }

        throw new ForbiddenException("Bu islem icin runbook sahibi olmaniz gerekiyor.");
    }

    private async Task<bool> IsCollaboratorAsync(Guid runbookId, string permission, CancellationToken ct)
    {
        if (!CollaboratorPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var userId = currentUser.UserId;
        if (userId is null || currentUser.IsImpersonating)
        {
            return false;
        }

        return await db.RunbookCollaborators.AnyAsync(c => c.RunbookId == runbookId && c.UserId == userId.Value, ct);
    }

    private bool HasPermission(string permission) => Permissions.Has(currentUser.Role, permission);

    private static ForbiddenException Forbidden(string permission)
        => new($"Bu islem icin '{permission}' yetkisi, runbook sahipligi veya editor yetkisi gerekiyor.");
}
