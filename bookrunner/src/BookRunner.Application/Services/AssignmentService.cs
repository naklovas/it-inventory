using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Services;

/// <summary>
/// Gorev atama ve devir islemleri. Atamalar kisiye veya dogrudan AD grubuna yapilir;
/// devir sirasinda eski atama silinmez, pasife alinip zincir korunur.
/// </summary>
public sealed class AssignmentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRunbookAccess access,
    IDirectorySyncService directorySync,
    IAuditService audit,
    INotificationService notifications,
    IRealtimeNotifier realtime) : IAssignmentService
{
    public async Task<TaskAssignmentDto> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct = default)
    {
        // Atama yetkisi rolden ya da runbook sahipliginden gelir.
        await access.EnsureForTaskAsync(taskId, Permissions.TaskAssign, ct);

        var task = await LoadTaskAsync(taskId, ct);
        var (userId, groupId) = await ResolveTargetAsync(request.AssigneeType, request.UserSid, request.UserId, request.GroupSid, request.GroupId, ct);

        var duplicate = await db.Assignments.AnyAsync(a =>
            a.TaskId == taskId && a.IsActive &&
            ((userId.HasValue && a.UserId == userId) || (groupId.HasValue && a.GroupId == groupId)), ct);

        if (duplicate)
        {
            throw new BusinessRuleException("Bu kisi/grup zaten bu goreve atanmis.");
        }

        var assignment = new TaskAssignment
        {
            TaskId = taskId,
            AssigneeType = request.AssigneeType,
            UserId = userId,
            GroupId = groupId,
            IsActive = true,
            HandoverNote = request.Note
        };

        db.Assignments.Add(assignment);

        var targetName = await DescribeTargetAsync(userId, groupId, ct);
        db.Activities.Add(NewActivity(taskId, TaskActivityType.Assigned, $"{targetName} goreve atandi.", newValue: targetName));

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(TaskAssignment), assignment.Id.ToString(),
            $"'{task.Title}' gorevi {targetName} kaydina atandi.", task.RunbookId, ct: ct);

        if (request.Notify)
        {
            await notifications.NotifyTaskAssignedAsync(taskId, assignment.Id, ct);
        }

        await realtime.TaskChangedAsync(task.RunbookId, taskId, "assigned", ct);

        return await LoadAssignmentDtoAsync(assignment.Id, ct);
    }

    public async Task<TaskAssignmentDto> HandoverAsync(Guid taskId, HandoverTaskRequest request, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskId, ct);

        var source = await db.Assignments
            .Include(a => a.User)
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Id == request.FromAssignmentId && a.TaskId == taskId, ct)
            ?? throw new NotFoundException("Atama", request.FromAssignmentId);

        if (!source.IsActive)
        {
            throw new BusinessRuleException("Bu atama zaten devredilmis veya kaldirilmis.");
        }

        await RequireHandoverPermissionAsync(source, ct);

        var (userId, groupId) = await ResolveTargetAsync(
            request.TargetType, request.TargetUserSid, request.TargetUserId, request.TargetGroupSid, request.TargetGroupId, ct);

        if (source.UserId == userId && source.GroupId == groupId)
        {
            throw new BusinessRuleException("Gorev ayni kisiye/gruba devredilemez.");
        }

        source.IsActive = false;
        source.ReleasedAt = DateTimeOffset.UtcNow;

        var target = new TaskAssignment
        {
            TaskId = taskId,
            AssigneeType = request.TargetType,
            UserId = userId,
            GroupId = groupId,
            IsActive = true,
            HandedOverFromAssignmentId = source.Id,
            HandoverNote = request.Note
        };

        db.Assignments.Add(target);

        var fromName = DescribeAssignment(source);
        var toName = await DescribeTargetAsync(userId, groupId, ct);

        db.Activities.Add(NewActivity(taskId, TaskActivityType.HandedOver,
            $"Gorev {fromName} -> {toName} devredildi. Not: {request.Note.Trim()}", fromName, toName));

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(TaskAssignment), target.Id.ToString(),
            $"'{task.Title}' gorevi {fromName} kaydindan {toName} kaydina devredildi.", task.RunbookId, ct: ct);

        await notifications.NotifyTaskHandedOverAsync(taskId, target.Id, request.Note, ct);
        await realtime.TaskChangedAsync(task.RunbookId, taskId, "handover", ct);

        return await LoadAssignmentDtoAsync(target.Id, ct);
    }

    public async Task RemoveAsync(Guid taskId, Guid assignmentId, CancellationToken ct = default)
    {
        await access.EnsureForTaskAsync(taskId, Permissions.TaskAssign, ct);

        var task = await LoadTaskAsync(taskId, ct);

        var assignment = await db.Assignments
            .Include(a => a.User)
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TaskId == taskId, ct)
            ?? throw new NotFoundException("Atama", assignmentId);

        if (!assignment.IsActive)
        {
            return;
        }

        assignment.IsActive = false;
        assignment.ReleasedAt = DateTimeOffset.UtcNow;

        var name = DescribeAssignment(assignment);
        db.Activities.Add(NewActivity(taskId, TaskActivityType.Unassigned, $"{name} atamasi kaldirildi.", oldValue: name));

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(TaskAssignment), assignmentId.ToString(),
            $"'{task.Title}' gorevinden {name} atamasi kaldirildi.", task.RunbookId, ct: ct);

        await realtime.TaskChangedAsync(task.RunbookId, taskId, "unassigned", ct);
    }

    public async Task<IReadOnlyList<TaskAssignmentDto>> ListAsync(Guid taskId, bool includeInactive, CancellationToken ct = default)
    {
        var query = db.Assignments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Group)
            .Where(a => a.TaskId == taskId);

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        var assignments = await query.OrderBy(a => a.CreatedAt).ToListAsync(ct);
        return assignments.Select(a => a.ToDto()).ToList();
    }

    /// <summary>Istek icindeki SID veya yerel kimlikten hedef kisi/grubu cozer.</summary>
    private async Task<(Guid? UserId, Guid? GroupId)> ResolveTargetAsync(
        AssigneeType type, string? userSid, Guid? userId, string? groupSid, Guid? groupId, CancellationToken ct)
    {
        if (type == AssigneeType.User)
        {
            if (userId.HasValue)
            {
                if (!await db.Users.AnyAsync(u => u.Id == userId.Value, ct))
                {
                    throw new NotFoundException("Kullanici", userId.Value);
                }

                return (userId.Value, null);
            }

            if (string.IsNullOrWhiteSpace(userSid))
            {
                throw ValidationException.Single(nameof(AssignTaskRequest.UserSid),
                    "Kisi atamasi icin kullanici SID veya kimligi gereklidir.");
            }

            var user = await directorySync.EnsureUserBySidAsync(userSid, ct);
            return (user.Id, null);
        }

        if (groupId.HasValue)
        {
            if (!await db.Groups.AnyAsync(g => g.Id == groupId.Value, ct))
            {
                throw new NotFoundException("Grup", groupId.Value);
            }

            return (null, groupId.Value);
        }

        if (string.IsNullOrWhiteSpace(groupSid))
        {
            throw ValidationException.Single(nameof(AssignTaskRequest.GroupSid),
                "Grup atamasi icin grup SID veya kimligi gereklidir.");
        }

        var group = await directorySync.EnsureGroupBySidAsync(groupSid, ct);
        return (null, group.Id);
    }

    private async Task<RunbookTask> LoadTaskAsync(Guid taskId, CancellationToken ct)
        => await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
           ?? throw new NotFoundException("Gorev", taskId);

    private async Task<TaskAssignmentDto> LoadAssignmentDtoAsync(Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.Assignments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Group)
            .FirstAsync(a => a.Id == assignmentId, ct);

        return assignment.ToDto();
    }

    private async Task<string> DescribeTargetAsync(Guid? userId, Guid? groupId, CancellationToken ct)
    {
        if (userId.HasValue)
        {
            return await db.Users.Where(u => u.Id == userId.Value).Select(u => u.DisplayName).FirstAsync(ct);
        }

        if (groupId.HasValue)
        {
            return await db.Groups.Where(g => g.Id == groupId.Value).Select(g => g.Name).FirstAsync(ct);
        }

        return "Bilinmeyen";
    }

    private static string DescribeAssignment(TaskAssignment assignment)
        => assignment.AssigneeType == AssigneeType.User
            ? assignment.User?.DisplayName ?? "Bilinmeyen kullanici"
            : assignment.Group?.Name ?? "Bilinmeyen grup";

    private TaskActivity NewActivity(Guid taskId, TaskActivityType type, string summary, string? oldValue = null, string? newValue = null)
        => new()
        {
            TaskId = taskId,
            Type = type,
            ActorUserId = currentUser.UserId,
            ActorDisplayName = currentUser.DisplayName,
            OldValue = oldValue,
            NewValue = newValue,
            Summary = summary
        };

    /// <summary>
    /// Devir kurali: atama yetkisi olanlar her atamayi devredebilir; digerleri
    /// yalnizca kendilerine ya da uyesi olduklari gruba ait atamayi devredebilir.
    /// </summary>
    private async Task RequireHandoverPermissionAsync(TaskAssignment source, CancellationToken ct)
    {
        if (Permissions.Has(currentUser.Role, Permissions.TaskAssign))
        {
            return;
        }

        // Runbook sahibi kendi runbook'undaki atamalari devredebilir.
        if (await access.IsOwnerOfTaskAsync(source.TaskId, ct))
        {
            return;
        }

        if (!Permissions.Has(currentUser.Role, Permissions.TaskExecute))
        {
            throw new ForbiddenException("Gorev devretme yetkiniz yok.");
        }

        var userId = currentUser.UserId;
        if (userId is null)
        {
            throw new ForbiddenException("Kullanici Active Directory'den cozulemedi.");
        }

        if (source.UserId == userId)
        {
            return;
        }

        if (source.GroupId.HasValue &&
            await db.UserGroups.AnyAsync(ug => ug.UserId == userId.Value && ug.GroupId == source.GroupId.Value, ct))
        {
            return;
        }

        throw new ForbiddenException("Yalnizca size veya grubunuza atanmis gorevleri devredebilirsiniz.");
    }
}
