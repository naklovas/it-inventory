using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Services;

/// <summary>
/// Gorev is kurallari. Her degisiklik ayrica bir <see cref="TaskActivity"/> kaydi
/// uretir; arayuzdeki akordiyon tarihce bu kayitlardan beslenir.
/// </summary>
public sealed class TaskService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRunbookAccess access,
    IAuditService audit,
    INotificationService notifications,
    IRealtimeNotifier realtime,
    IExternalIntegrationClient integration,
    IGamificationService gamification) : ITaskService
{
    public async Task<RunbookTaskDto> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await LoadAsync(taskId, tracking: false, ct)
            ?? throw new NotFoundException("Gorev", taskId);

        return task.ToDto();
    }

    public async Task<RunbookTaskDto> CreateAsync(Guid runbookId, CreateTaskRequest request, CancellationToken ct = default)
    {
        await access.EnsureForRunbookAsync(runbookId, Permissions.TaskWrite, ct);

        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == runbookId, ct)
            ?? throw new NotFoundException("Runbook", runbookId);

        if (runbook.Status is RunbookStatus.Completed or RunbookStatus.Cancelled or RunbookStatus.Archived)
        {
            throw new BusinessRuleException("Kapanmis bir runbook'a yeni gorev eklenemez.");
        }

        ValidateTaskPlannedRange(runbook, request.PlannedStart, request.PlannedEnd);

        var maxOrder = await db.Tasks.Where(t => t.RunbookId == runbookId).MaxAsync(t => (int?)t.Order, ct) ?? 0;
        var order = request.Order is > 0 ? request.Order.Value : maxOrder + 1;

        if (request.Order is > 0)
        {
            // Araya eklenirken sonraki gorevlerin sirasi bir kaydirilir.
            var shifted = await db.Tasks.Where(t => t.RunbookId == runbookId && t.Order >= order).ToListAsync(ct);
            foreach (var item in shifted)
            {
                item.Order++;
            }
        }

        var task = new RunbookTask
        {
            RunbookId = runbookId,
            Order = order,
            Title = request.Title.Trim(),
            Description = request.Description,
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? AvatarHelper.TaskColor(order) : request.ColorHex!,
            Priority = request.Priority,
            EstimatedMinutes = request.EstimatedMinutes,
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            RollbackNotes = request.RollbackNotes
        };

        var dependsOnIds = request.DependsOnTaskIds.Distinct().ToList();
        await ValidateDependenciesAsync(runbookId, task.Id, dependsOnIds, ct);

        db.Tasks.Add(task);
        foreach (var dependsOnId in dependsOnIds)
        {
            db.TaskDependencies.Add(new TaskDependency { TaskId = task.Id, DependsOnTaskId = dependsOnId });
        }

        await db.SaveChangesAsync(ct);

        AddActivity(task.Id, TaskActivityType.Created, $"'{task.Title}' gorevi olusturuldu.");
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Create, nameof(RunbookTask), task.Id.ToString(),
            $"'{task.Title}' gorevi eklendi.", runbookId, ct: ct);

        await realtime.TaskChangedAsync(runbookId, task.Id, "created", ct);

        return await GetAsync(task.Id, ct);
    }

    public async Task<RunbookTaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        await access.EnsureForTaskAsync(taskId, Permissions.TaskWrite, ct);

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException("Gorev", taskId);

        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == task.RunbookId, ct)
            ?? throw new NotFoundException("Runbook", task.RunbookId);

        ValidateTaskPlannedRange(runbook, request.PlannedStart, request.PlannedEnd);

        var dependsOnIds = request.DependsOnTaskIds.Distinct().ToList();
        await ValidateDependenciesAsync(task.RunbookId, taskId, dependsOnIds, ct);

        if (request.ScriptId.HasValue &&
            !await db.Scripts.AnyAsync(s => s.Id == request.ScriptId.Value, ct))
        {
            throw new NotFoundException("Script", request.ScriptId.Value);
        }

        var changes = new List<string>();
        if (!string.Equals(task.Title, request.Title.Trim(), StringComparison.Ordinal))
        {
            changes.Add($"Baslik: '{task.Title}' -> '{request.Title.Trim()}'");
        }

        if (task.Priority != request.Priority)
        {
            changes.Add($"Oncelik: {DisplayText.Priority(task.Priority)} -> {DisplayText.Priority(request.Priority)}");
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Priority = request.Priority;
        task.EstimatedMinutes = request.EstimatedMinutes;
        task.PlannedStart = request.PlannedStart;
        task.PlannedEnd = request.PlannedEnd;
        task.RollbackNotes = request.RollbackNotes;
        task.ScriptId = request.ScriptId;

        if (!string.IsNullOrWhiteSpace(request.ColorHex))
        {
            task.ColorHex = request.ColorHex!;
        }

        var existingDependencies = await db.TaskDependencies.Where(d => d.TaskId == taskId).ToListAsync(ct);
        db.TaskDependencies.RemoveRange(existingDependencies);
        foreach (var dependsOnId in dependsOnIds)
        {
            db.TaskDependencies.Add(new TaskDependency { TaskId = taskId, DependsOnTaskId = dependsOnId });
        }

        AddActivity(task.Id, TaskActivityType.Updated,
            changes.Count > 0 ? string.Join(" | ", changes) : "Gorev detaylari guncellendi.");

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(RunbookTask), task.Id.ToString(),
            $"'{task.Title}' gorevi guncellendi.", task.RunbookId, ct: ct);

        await realtime.TaskChangedAsync(task.RunbookId, task.Id, "updated", ct);

        return await GetAsync(task.Id, ct);
    }

    public async Task<RunbookTaskDto> ChangeStatusAsync(Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
    {
        var task = await db.Tasks
            .Include(t => t.Assignments)
            .Include(t => t.Runbook)
            .Include(t => t.Predecessors).ThenInclude(d => d.DependsOnTask)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException("Gorev", taskId);

        await RequireExecutePermissionAsync(task, ct);

        if (task.Status == request.Status)
        {
            return await GetAsync(taskId, ct);
        }

        if (request.Status is RunbookTaskStatus.InProgress or RunbookTaskStatus.Completed)
        {
            // Siki kural: ADIL, TUM oncelleri kapanmadan (Tamamlandi/Atlandi)
            // baslatilamaz/tamamlanamaz - tek bir acik oncul bile yeterlidir.
            var openPredecessors = task.Predecessors
                .Where(d => !d.DependsOnTask.Status.IsClosed())
                .Select(d => d.DependsOnTask.Title)
                .ToList();

            if (openPredecessors.Count > 0)
            {
                throw new BusinessRuleException(
                    $"Bu gorev baslatilamaz: once su oncul gorev(ler) tamamlanmali: {string.Join(", ", openPredecessors)}.");
            }
        }

        var oldStatus = task.Status;
        var now = DateTimeOffset.UtcNow;

        task.Status = request.Status;
        switch (request.Status)
        {
            case RunbookTaskStatus.InProgress:
                task.ActualStart ??= now;
                task.ActualEnd = null;
                break;
            case RunbookTaskStatus.Completed:
            case RunbookTaskStatus.Failed:
            case RunbookTaskStatus.Skipped:
                task.ActualStart ??= now;
                task.ActualEnd = now;
                break;
        }

        var activityType = request.Status switch
        {
            RunbookTaskStatus.InProgress => TaskActivityType.Started,
            RunbookTaskStatus.Completed => TaskActivityType.Completed,
            RunbookTaskStatus.Blocked => TaskActivityType.Blocked,
            _ => TaskActivityType.StatusChanged
        };

        var summary = $"Durum {DisplayText.Status(oldStatus)} -> {DisplayText.Status(request.Status)}";
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            summary += $" ({request.Note.Trim()})";
        }

        AddActivity(task.Id, activityType, summary, DisplayText.Status(oldStatus), DisplayText.Status(request.Status));

        // Ilk gorev baslayinca runbook da otomatik olarak "Devam Ediyor" olur.
        if (request.Status == RunbookTaskStatus.InProgress &&
            task.Runbook.Status is RunbookStatus.Draft or RunbookStatus.Scheduled)
        {
            task.Runbook.Status = RunbookStatus.InProgress;
            task.Runbook.ActualStart ??= now;
        }

        await db.SaveChangesAsync(ct);

        if (request.Status is RunbookTaskStatus.Completed or RunbookTaskStatus.Failed && currentUser.UserId is { } actorId)
        {
            await gamification.OnTaskClosedAsync(task, actorId, ct);
        }

        // Son gorev de kapaninca (Completed/Skipped) runbook otomatik "Tamamlandi"
        // olur - ilk gorev baslayinca otomatik "Devam Ediyor" olmasiyla simetrik.
        // Bloke/basarisiz gorev varsa otomatik kapatilmaz; bu, bir operatorun
        // bilerek karar vermesini gerektirir.
        if (request.Status is RunbookTaskStatus.Completed or RunbookTaskStatus.Skipped)
        {
            await TryAutoCompleteRunbookAsync(task.RunbookId, ct);
        }

        await audit.LogAsync(AuditAction.Update, nameof(RunbookTask), task.Id.ToString(), summary, task.RunbookId, ct: ct);
        await notifications.NotifyTaskStatusChangedAsync(task.Id, DisplayText.Status(oldStatus), DisplayText.Status(request.Status), ct);
        await realtime.TaskChangedAsync(task.RunbookId, task.Id, "status", ct);

        if (integration.IsEnabled)
        {
            await integration.PublishEventAsync(new ExternalEvent
            {
                EventType = "task.status-changed",
                RunbookId = task.RunbookId,
                RunbookCode = task.Runbook.Code,
                RunbookTitle = task.Runbook.Title,
                TaskId = task.Id,
                TaskTitle = task.Title,
                Status = DisplayText.Status(task.Status),
                ActorDisplayName = currentUser.DisplayName,
                Message = summary
            }, ct);
        }

        return await GetAsync(task.Id, ct);
    }

    public async Task ReorderAsync(Guid runbookId, ReorderTasksRequest request, CancellationToken ct = default)
    {
        await access.EnsureForRunbookAsync(runbookId, Permissions.TaskWrite, ct);

        var tasks = await db.Tasks.Where(t => t.RunbookId == runbookId).ToListAsync(ct);
        var byId = tasks.ToDictionary(t => t.Id);

        if (request.TaskIdsInOrder.Count != tasks.Count || request.TaskIdsInOrder.Any(id => !byId.ContainsKey(id)))
        {
            throw new BusinessRuleException("Siralama listesi runbook'taki gorevlerle birebir eslesmiyor.");
        }

        for (var index = 0; index < request.TaskIdsInOrder.Count; index++)
        {
            var task = byId[request.TaskIdsInOrder[index]];
            var newOrder = index + 1;
            if (task.Order != newOrder)
            {
                AddActivity(task.Id, TaskActivityType.Reordered, $"Sira {task.Order} -> {newOrder}");
                task.Order = newOrder;
            }
        }

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(Runbook), runbookId.ToString(),
            "Gorev sirasi degistirildi.", runbookId, ct: ct);

        await realtime.RunbookChangedAsync(runbookId, "reordered", ct);
    }

    public async Task DeleteAsync(Guid taskId, CancellationToken ct = default)
    {
        // Gorev silme yetkisi yonetici rolunde; runbook sahibi de kendi gorevlerini silebilir.
        await access.EnsureForTaskAsync(taskId, Permissions.TaskDelete, ct);

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException("Gorev", taskId);

        if (task.Status == RunbookTaskStatus.InProgress)
        {
            throw new BusinessRuleException("Devam eden bir gorev silinemez.");
        }

        // Bu gorevin hem oncul hem ardil olarak yer aldigi tum bagimlilik
        // kayitlari silinir; ardillar bu gorevi bekliyordu, artik beklemezler.
        var relatedDependencies = await db.TaskDependencies
            .Where(d => d.TaskId == taskId || d.DependsOnTaskId == taskId)
            .ToListAsync(ct);
        db.TaskDependencies.RemoveRange(relatedDependencies);

        task.IsDeleted = true;
        task.DeletedAt = DateTimeOffset.UtcNow;
        task.DeletedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Delete, nameof(RunbookTask), task.Id.ToString(),
            $"'{task.Title}' gorevi silindi.", task.RunbookId, ct: ct);

        await realtime.TaskChangedAsync(task.RunbookId, task.Id, "deleted", ct);
    }

    public async Task<IReadOnlyList<TaskActivityDto>> GetHistoryAsync(Guid taskId, CancellationToken ct = default)
    {
        if (!await db.Tasks.AnyAsync(t => t.Id == taskId, ct))
        {
            throw new NotFoundException("Gorev", taskId);
        }

        var activities = await db.Activities
            .AsNoTracking()
            .Include(a => a.Actor)
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        return activities.Select(a => a.ToDto()).ToList();
    }

    private async Task<RunbookTask?> LoadAsync(Guid taskId, bool tracking, CancellationToken ct)
    {
        var query = db.Tasks
            .Include(t => t.Assignments).ThenInclude(a => a.User)
            .Include(t => t.Assignments).ThenInclude(a => a.Group)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.Activities)
            .Include(t => t.Script)
            .Include(t => t.Predecessors).ThenInclude(d => d.DependsOnTask)
            .Include(t => t.Successors).ThenInclude(d => d.Task)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(t => t.Id == taskId, ct);
    }

    /// <summary>
    /// Bir gorevin planlanan tarih araligi runbook'un planlanan araliginin
    /// disina tasamaz. Runbook henuz tarihlendirilmemisse (eski kayitlar,
    /// yeni runbook'larda artik zorunlu) gorev tarihi girilemez - once
    /// runbook'un kendi tarihi girilmelidir.
    /// </summary>
    private static void ValidateTaskPlannedRange(Runbook runbook, DateTimeOffset? start, DateTimeOffset? end)
    {
        if (end.HasValue && start.HasValue && end.Value < start.Value)
        {
            throw ValidationException.Single(nameof(CreateTaskRequest.PlannedEnd),
                "Planlanan bitis, planlanan baslangictan once olamaz.");
        }

        if (start is null && end is null)
        {
            return;
        }

        if (runbook.PlannedStart is null || runbook.PlannedEnd is null)
        {
            throw new BusinessRuleException(
                "Gorev tarihi girebilmek icin once runbook'un planlanan baslangic/bitis tarihini belirlemelisiniz.");
        }

        if (start.HasValue && start.Value < runbook.PlannedStart.Value)
        {
            throw ValidationException.Single(nameof(CreateTaskRequest.PlannedStart),
                $"Gorev baslangici runbook'un planlanan baslangicindan " +
                $"({runbook.PlannedStart.Value.ToLocalTime():dd.MM.yyyy HH:mm}) once olamaz.");
        }

        if (end.HasValue && end.Value > runbook.PlannedEnd.Value)
        {
            throw ValidationException.Single(nameof(CreateTaskRequest.PlannedEnd),
                $"Gorev bitisi runbook'un planlanan bitisini " +
                $"({runbook.PlannedEnd.Value.ToLocalTime():dd.MM.yyyy HH:mm}) asamaz.");
        }
    }

    /// <summary>
    /// Secilen oncul kimliklerinin gecerliligini (kendine referans yok, ayni
    /// runbook icinde) ve bu degisikligin bagimlilik dongusu olusturmadigini
    /// dogrular. Hem CreateAsync (henuz kaydedilmemis ama Id'si atanmis yeni
    /// gorev) hem UpdateAsync (mevcut gorevin TUM oncul kumesini degistirir)
    /// tarafindan kullanilir.
    /// </summary>
    private async Task ValidateDependenciesAsync(
        Guid runbookId, Guid taskId, IReadOnlyList<Guid> dependsOnTaskIds, CancellationToken ct)
    {
        if (dependsOnTaskIds.Contains(taskId))
        {
            throw ValidationException.Single(nameof(CreateTaskRequest.DependsOnTaskIds), "Bir gorev kendisine oncul olamaz.");
        }

        if (dependsOnTaskIds.Count == 0)
        {
            return;
        }

        var validIds = await db.Tasks
            .Where(t => t.RunbookId == runbookId && dependsOnTaskIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (validIds.Count != dependsOnTaskIds.Count)
        {
            throw ValidationException.Single(nameof(CreateTaskRequest.DependsOnTaskIds),
                "Secilen oncul gorevlerden biri veya birkaci ayni runbook icinde bulunamadi.");
        }

        // Dongu kontrolu: bu gorevin KENDI mevcut baglarini haric tutarak
        // (onlarin yerine yenileri gececek) runbook'taki tum bagimliliklardan
        // bir komsuluk haritasi cikarilir; her yeni oncul adayindan bu goreve
        // (dolayli da olsa) zaten bir yol varsa, ekleme bir dongu olusturur.
        var existingEdges = await db.TaskDependencies
            .Where(d => d.Task.RunbookId == runbookId && d.TaskId != taskId)
            .Select(d => new { d.TaskId, d.DependsOnTaskId })
            .ToListAsync(ct);

        var adjacency = existingEdges
            .GroupBy(e => e.TaskId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.DependsOnTaskId).ToList());

        foreach (var candidateId in dependsOnTaskIds)
        {
            if (IsReachable(candidateId, taskId, adjacency))
            {
                throw new BusinessRuleException(
                    "Bu bagimlilik bir dongu olusturuyor: secilen oncul, dolayli olarak zaten bu goreve bagli.");
            }
        }
    }

    /// <summary>"Oncul" kenarlari (TaskId -> DependsOnTaskId) uzerinden derinlik-oncelikli arama ile "target"a ulasilabiliyor mu.</summary>
    private static bool IsReachable(Guid from, Guid target, IReadOnlyDictionary<Guid, List<Guid>> adjacency)
    {
        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(from);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == target)
            {
                return true;
            }

            if (!visited.Add(current) || !adjacency.TryGetValue(current, out var deps))
            {
                continue;
            }

            foreach (var dep in deps)
            {
                stack.Push(dep);
            }
        }

        return false;
    }

    private void AddActivity(Guid taskId, TaskActivityType type, string summary, string? oldValue = null, string? newValue = null)
        => db.Activities.Add(new TaskActivity
        {
            TaskId = taskId,
            Type = type,
            ActorUserId = currentUser.UserId,
            ActorDisplayName = currentUser.DisplayName,
            OldValue = oldValue,
            NewValue = newValue,
            Summary = summary
        });

    /// <summary>
    /// Durum degistirmek icin ya gorev yazma yetkisi ya da goreve (dogrudan veya
    /// grubu uzerinden) atanmis olmak gerekir.
    /// </summary>
    private async Task RequireExecutePermissionAsync(RunbookTask task, CancellationToken ct)
    {
        if (Permissions.Has(currentUser.Role, Permissions.TaskWrite))
        {
            return;
        }

        // Runbook sahibi kendi runbook'undaki her gorevi ilerletebilir.
        if (await access.IsOwnerOfTaskAsync(task.Id, ct))
        {
            return;
        }

        if (!Permissions.Has(currentUser.Role, Permissions.TaskExecute))
        {
            throw new ForbiddenException("Gorev durumunu degistirme yetkiniz yok.");
        }

        if (await IsAssignedToCurrentUserAsync(task, ct))
        {
            return;
        }

        throw new ForbiddenException("Yalnizca size veya grubunuza atanmis gorevlerin durumunu degistirebilirsiniz.");
    }

    private async Task<bool> IsAssignedToCurrentUserAsync(RunbookTask task, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return false;
        }

        if (task.Assignments.Any(a => a.IsActive && a.UserId == userId))
        {
            return true;
        }

        var groupIds = task.Assignments.Where(a => a.IsActive && a.GroupId.HasValue).Select(a => a.GroupId!.Value).ToList();
        if (groupIds.Count == 0)
        {
            return false;
        }

        return await db.UserGroups.AnyAsync(ug => ug.UserId == userId.Value && groupIds.Contains(ug.GroupId), ct);
    }

    /// <summary>
    /// Runbook'un TUM gorevleri kapandiginda (Completed/Skipped) runbook'u da
    /// otomatik olarak Tamamlandi yapar. Bloke veya basarisiz bir gorev varsa
    /// otomatik kapatmaz - bu, kasitli bir operator kararini gerektirir.
    /// </summary>
    private async Task TryAutoCompleteRunbookAsync(Guid runbookId, CancellationToken ct)
    {
        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == runbookId, ct);
        if (runbook is null || runbook.Status is RunbookStatus.Completed or RunbookStatus.Cancelled or RunbookStatus.Archived)
        {
            return;
        }

        var taskStatuses = await db.Tasks.Where(t => t.RunbookId == runbookId).Select(t => t.Status).ToListAsync(ct);
        if (taskStatuses.Count == 0 || taskStatuses.Any(status => !status.IsClosed()))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        runbook.Status = RunbookStatus.Completed;
        runbook.ActualStart ??= now;
        runbook.ActualEnd = now;

        await db.SaveChangesAsync(ct);

        await gamification.OnRunbookCompletedAsync(runbook, ct);
        await audit.LogAsync(AuditAction.Update, nameof(Runbook), runbook.Id.ToString(),
            "Tum gorevler tamamlandigi icin runbook otomatik olarak kapatildi.", runbook.Id, ct: ct);
        await realtime.RunbookChangedAsync(runbook.Id, "updated", ct);
    }
}
