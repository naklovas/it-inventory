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

        var scenarioGroup = string.IsNullOrWhiteSpace(request.ScenarioGroup) ? null : request.ScenarioGroup.Trim();
        var scenarioRejoinTaskId = scenarioGroup is not null ? request.ScenarioRejoinTaskId : null;
        if (scenarioRejoinTaskId.HasValue)
        {
            await ValidateScenarioRejoinTaskAsync(runbookId, scenarioRejoinTaskId.Value, ct);
        }

        // Ana akis, geri donus adimlari ve her senaryo grubu ayri listeler olarak
        // numaralanir (bkz. RunbookTask.IsRollbackStep, RunbookTask.ScenarioGroup) -
        // biri digerinin sira numarasindan etkilenmez.
        var maxOrder = await db.Tasks
            .Where(t => t.RunbookId == runbookId
                && t.IsRollbackStep == request.IsRollbackStep
                && t.ScenarioGroup == scenarioGroup)
            .MaxAsync(t => (int?)t.Order, ct) ?? 0;
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
            RollbackNotes = request.RollbackNotes,
            IsOutageStep = request.IsOutageStep,
            PlannedOutageMinutes = request.IsOutageStep ? request.PlannedOutageMinutes : null,
            IsRollbackStep = request.IsRollbackStep,
            ScenarioGroup = scenarioGroup,
            ScenarioRejoinTaskId = scenarioRejoinTaskId,
            FailureAction = request.FailureAction,
            FailureScenarioGroup = request.FailureAction == TaskFailureAction.SwitchToScenario
                ? request.FailureScenarioGroup?.Trim()
                : null,
            SuccessScenarioGroup = string.IsNullOrWhiteSpace(request.SuccessScenarioGroup)
                ? null
                : request.SuccessScenarioGroup.Trim()
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
        task.IsOutageStep = request.IsOutageStep;
        task.PlannedOutageMinutes = request.IsOutageStep ? request.PlannedOutageMinutes : null;
        if (!request.IsOutageStep)
        {
            task.ActualOutageMinutes = null;
        }
        task.IsRollbackStep = request.IsRollbackStep;
        task.ScenarioGroup = string.IsNullOrWhiteSpace(request.ScenarioGroup) ? null : request.ScenarioGroup.Trim();
        var scenarioRejoinTaskId = task.ScenarioGroup is not null ? request.ScenarioRejoinTaskId : null;
        if (scenarioRejoinTaskId.HasValue)
        {
            await ValidateScenarioRejoinTaskAsync(task.RunbookId, scenarioRejoinTaskId.Value, ct);
        }
        task.ScenarioRejoinTaskId = scenarioRejoinTaskId;
        task.FailureAction = request.FailureAction;
        task.FailureScenarioGroup = request.FailureAction == TaskFailureAction.SwitchToScenario
            ? request.FailureScenarioGroup?.Trim()
            : null;
        task.SuccessScenarioGroup = string.IsNullOrWhiteSpace(request.SuccessScenarioGroup)
            ? null
            : request.SuccessScenarioGroup.Trim();

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

        // Gorevi baslangic haline sifirlamak (gerceklesen sure/not, ActualStart/End
        // dahil her sey silinir) yalnizca yonetici rolunde - istisnai/duzeltici bir
        // islemdir, arayuzde de yalnizca yoneticiye gosterilir.
        if (request.Status == RunbookTaskStatus.NotStarted && !Permissions.Has(currentUser.Role, Permissions.AdminManage))
        {
            throw new ForbiddenException("Gorevi baslangic durumuna dondurme yetkisi yalnizca yoneticidedir.");
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
            case RunbookTaskStatus.NotApplicable:
                task.ActualStart ??= now;
                task.ActualEnd = now;
                break;
            case RunbookTaskStatus.NotStarted:
                // Gorevi ilk haline dondurur: gerceklesen tarihler ve tamamlama
                // bilgileri de silinir, yoksa bir sonraki gecişte yanlis bilgi kalir.
                task.ActualStart = null;
                task.ActualEnd = null;
                task.ActualMinutes = null;
                task.ActualOutageMinutes = null;
                task.CompletionNote = null;
                break;
        }

        // Tamamlanma modalindan gelen gercek sure/not: yalnizca Tamamlandi'ya
        // ozgudur, arayuz da bu alanlari yalnizca o gecişte gosterir.
        if (request.Status == RunbookTaskStatus.Completed)
        {
            if (request.ActualMinutes.HasValue)
            {
                task.ActualMinutes = request.ActualMinutes;
            }

            if (task.IsOutageStep && request.ActualOutageMinutes.HasValue)
            {
                task.ActualOutageMinutes = request.ActualOutageMinutes;
            }

            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                task.CompletionNote = request.Note.Trim();
            }
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

        // Simetrik durum: TryAutoCompleteRunbookAsync son gorev kapaninca runbook'u
        // otomatik "Tamamlandi" yapiyordu. Bir yonetici o gorevi (veya baska bir
        // kapali gorevi) tekrar acik bir duruma geri cekerse, runbook'un "Tamamlandi"
        // gibi gorunmeye devam etmesi yaniltici olur - alt adimlarin gercek durumunu
        // yansitmasi icin otomatik olarak "Devam Ediyor"a donduruluyor.
        if (!request.Status.IsClosed() && task.Runbook.Status == RunbookStatus.Completed)
        {
            task.Runbook.Status = RunbookStatus.InProgress;
            task.Runbook.ActualEnd = null;
        }

        // Gorev bazinda tanimlanmis "basarili/basarisiz olursa ne olsun" davranisi
        // (bkz. RunbookTask.FailureAction, RunbookTask.SuccessScenarioGroup):
        // operatorun ayrica "Geri Donus Adimlarini Baslat"/"X Senaryosuna Gec"
        // butonuna basmasina gerek kalmadan otomatik tetiklenir. Hedef zaten
        // aktifse veya tanimli degilse (rollback adimi/senaryo yoksa) sessizce
        // atlanir - bu otomatik adim asla hata firlatip durum degisikligini
        // engellemez.
        var hasFailureTrigger = request.Status == RunbookTaskStatus.Failed && task.FailureAction != TaskFailureAction.None;
        var hasSuccessTrigger = request.Status == RunbookTaskStatus.Completed && !string.IsNullOrWhiteSpace(task.SuccessScenarioGroup);

        if (hasFailureTrigger || hasSuccessTrigger)
        {
            var siblingTasks = await db.Tasks.Where(t => t.RunbookId == task.RunbookId).ToListAsync(ct);

            if (hasFailureTrigger && task.FailureAction == TaskFailureAction.StartRollback
                && !task.Runbook.IsRollbackActive
                && siblingTasks.Any(t => t.IsRollbackStep))
            {
                ActivateRollback(task.Runbook, siblingTasks,
                    $"'{task.Title}' basarisiz oldugu icin geri donus plani otomatik baslatildi");
            }
            else if (hasFailureTrigger && task.FailureAction == TaskFailureAction.SwitchToScenario
                && !task.Runbook.IsRollbackActive
                && string.IsNullOrEmpty(task.Runbook.ActiveScenarioGroup)
                && !string.IsNullOrWhiteSpace(task.FailureScenarioGroup)
                && siblingTasks.Any(t => !t.IsRollbackStep && t.ScenarioGroup == task.FailureScenarioGroup))
            {
                ActivateScenario(task.Runbook, siblingTasks, task.FailureScenarioGroup!,
                    $"'{task.Title}' basarisiz oldugu icin '{task.FailureScenarioGroup}' senaryosuna otomatik gecildi");
            }
            else if (hasSuccessTrigger
                && !task.Runbook.IsRollbackActive
                && string.IsNullOrEmpty(task.Runbook.ActiveScenarioGroup)
                && siblingTasks.Any(t => !t.IsRollbackStep && t.ScenarioGroup == task.SuccessScenarioGroup))
            {
                ActivateScenario(task.Runbook, siblingTasks, task.SuccessScenarioGroup!,
                    $"'{task.Title}' basarili oldugu icin '{task.SuccessScenarioGroup}' senaryosuna otomatik gecildi");
            }
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
        if (request.Status is RunbookTaskStatus.Completed or RunbookTaskStatus.Skipped or RunbookTaskStatus.NotApplicable)
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

    public async Task StartRollbackAsync(Guid runbookId, CancellationToken ct = default)
    {
        // Gorev yurutme yetkisiyle ayni seviye: runbook sahibi de (sahiplik
        // yoluyla) tetikleyebilir.
        await access.EnsureForRunbookAsync(runbookId, Permissions.TaskExecute, ct);

        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == runbookId, ct)
            ?? throw new NotFoundException("Runbook", runbookId);

        if (runbook.IsRollbackActive)
        {
            throw new BusinessRuleException("Geri donus plani zaten baslatilmis.");
        }

        var tasks = await db.Tasks.Where(t => t.RunbookId == runbookId).ToListAsync(ct);
        if (!tasks.Any(t => t.IsRollbackStep))
        {
            throw new BusinessRuleException("Bu runbook icin tanimlanmis bir geri donus adimi yok.");
        }

        // Yalnizca su an izlenen tek grup dikkate alinir: ana akis (henuz senaryo
        // secilmemisse) veya aktive edilmis senaryonun kendi adimlari. Baska,
        // hic secilmemis senaryo gruplarinin onceden yazilmis adimlari bu
        // kontrole dahil edilmez.
        var activeTrackTasks = tasks.Where(t => !t.IsRollbackStep && t.ScenarioGroup == runbook.ActiveScenarioGroup).ToList();
        if (!activeTrackTasks.Any(t => t.Status == RunbookTaskStatus.Failed))
        {
            throw new BusinessRuleException(
                "Geri donus plani yalnizca akista en az bir gorev 'Basarisiz' oldugunda baslatilabilir.");
        }

        ActivateRollback(runbook, tasks, "geri donus plani baslatildi");

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(Runbook), runbookId.ToString(),
            "Geri donus plani baslatildi.", runbookId, ct: ct);
        await realtime.RunbookChangedAsync(runbookId, "rollback-started", ct);
    }

    /// <summary>
    /// Geri donus planini aktive eder: aktif akistaki kapanmamis (Bekliyor/Devam
    /// Eden/Bloke) gorevler "Atlandi" olur, geri donus adimlarinin tarihleri
    /// simdiden itibaren sirayla hesaplanir. Cagiran taraf tum on kosullari
    /// (rollback adimi var mi, zaten aktif mi) onceden dogrulamis olmalidir -
    /// bu metot yalnizca uygular, ayrica dogrulama yapmaz (hem manuel buton hem
    /// gorev bazinda otomatik tetikleme buradan gecer).
    /// </summary>
    private void ActivateRollback(Runbook runbook, List<RunbookTask> tasks, string reason)
    {
        var rollbackSteps = tasks.Where(t => t.IsRollbackStep).OrderBy(t => t.Order).ToList();
        var activeTrackTasks = tasks.Where(t => !t.IsRollbackStep && t.ScenarioGroup == runbook.ActiveScenarioGroup).ToList();
        var now = DateTimeOffset.UtcNow;

        // Yalnizca gercekten "acik" (Bekliyor/Devam Eden/Bloke) gorevler Atlandi
        // olur. IsClosed() (Tamamlandi/Atlandi/N-A) burada YETERLI DEGIL - Failed
        // de kapanmis sayilmalidir, aksi halde bu dongu az once "Basarisiz"
        // isaretlenmis (rollback'i tetikleyen) gorevin durumunu sessizce
        // "Atlandi"ya cevirip asil sebebi gizlerdi.
        foreach (var task in activeTrackTasks.Where(t => t.Status is RunbookTaskStatus.NotStarted or RunbookTaskStatus.InProgress or RunbookTaskStatus.Blocked))
        {
            var oldStatus = task.Status;
            task.Status = RunbookTaskStatus.Skipped;
            task.ActualStart ??= now;
            task.ActualEnd = now;
            AddActivity(task.Id, TaskActivityType.StatusChanged,
                $"Durum {DisplayText.Status(oldStatus)} -> {DisplayText.Status(RunbookTaskStatus.Skipped)} ({reason})",
                DisplayText.Status(oldStatus), DisplayText.Status(RunbookTaskStatus.Skipped));
        }

        // Geri donus adimlarinin ne zaman tetiklenecegi onceden bilinemedigi
        // icin tarihleri simdi, sirayla (bir onceki adimin bitisine gore)
        // hesaplanir - yalnizca tahmini sureleriyle onceden girilmislerdi.
        var cursor = now;
        foreach (var step in rollbackSteps)
        {
            step.PlannedStart = cursor;
            cursor = step.EstimatedMinutes.HasValue ? cursor.AddMinutes(step.EstimatedMinutes.Value) : cursor;
            step.PlannedEnd = cursor;
        }

        runbook.IsRollbackActive = true;
        if (runbook.PlannedEnd is null || cursor > runbook.PlannedEnd.Value)
        {
            runbook.PlannedEnd = cursor;
        }
    }

    public async Task SwitchScenarioAsync(Guid runbookId, SwitchScenarioRequest request, CancellationToken ct = default)
    {
        // Gorev yurutme yetkisiyle ayni seviye: runbook sahibi de (sahiplik
        // yoluyla) tetikleyebilir.
        await access.EnsureForRunbookAsync(runbookId, Permissions.TaskExecute, ct);

        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == runbookId, ct)
            ?? throw new NotFoundException("Runbook", runbookId);

        if (runbook.IsRollbackActive)
        {
            throw new BusinessRuleException("Geri donus plani aktifken senaryo degistirilemez.");
        }

        if (!string.IsNullOrEmpty(runbook.ActiveScenarioGroup))
        {
            throw new BusinessRuleException(
                $"Bu calisma zaten '{runbook.ActiveScenarioGroup}' senaryosu uzerinden yuruyor.");
        }

        var scenarioGroup = request.ScenarioGroup.Trim();

        var tasks = await db.Tasks.Where(t => t.RunbookId == runbookId).ToListAsync(ct);
        if (!tasks.Any(t => !t.IsRollbackStep && t.ScenarioGroup == scenarioGroup))
        {
            throw new BusinessRuleException($"'{scenarioGroup}' adinda tanimli bir senaryo bulunamadi.");
        }

        ActivateScenario(runbook, tasks, scenarioGroup, $"'{scenarioGroup}' senaryosuna gecildi");

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(Runbook), runbookId.ToString(),
            $"'{scenarioGroup}' senaryosuna gecildi.", runbookId, ct: ct);
        await realtime.RunbookChangedAsync(runbookId, "scenario-switched", ct);
    }

    /// <summary>
    /// Belirtilen senaryoya gecisi aktive eder: ana akistaki kapanmamis (Bekliyor/
    /// Devam Eden/Bloke) gorevler "Atlandi" olur, senaryo adimlarinin tarihleri
    /// simdiden itibaren sirayla hesaplanir. Cagiran taraf tum on kosullari
    /// (senaryo tanimli mi, zaten aktif mi) onceden dogrulamis olmalidir - bu
    /// metot yalnizca uygular (hem manuel buton hem gorev bazinda otomatik
    /// tetikleme buradan gecer).
    /// </summary>
    private void ActivateScenario(Runbook runbook, List<RunbookTask> tasks, string scenarioGroup, string reason)
    {
        var scenarioSteps = tasks
            .Where(t => !t.IsRollbackStep && t.ScenarioGroup == scenarioGroup)
            .OrderBy(t => t.Order)
            .ToList();

        // Senaryo grubunun rejoin noktasi (bkz. RunbookTask.ScenarioRejoinTaskId):
        // gruptaki herhangi bir adimda dolu olmasi yeterlidir, tumune kopyalanmasi
        // gerekmez.
        var rejoinTaskId = scenarioSteps.Select(t => t.ScenarioRejoinTaskId).FirstOrDefault(id => id.HasValue);
        var rejoinOrder = rejoinTaskId.HasValue
            ? tasks.FirstOrDefault(t => t.Id == rejoinTaskId.Value)?.Order
            : null;

        // Senaryo secilmeden once izlenen ana akis (ScenarioGroup bos) - gecis
        // yalnizca ana akistan yapilabilir, bir senaryodan digerine degil.
        var mainTasks = tasks.Where(t => !t.IsRollbackStep && t.ScenarioGroup == null).ToList();

        var now = DateTimeOffset.UtcNow;

        // Rejoin noktasi tanimliysa yalnizca ONDAN ONCEKI acik ana akis gorevleri
        // Atlandi olur; rejoin noktasi ve sonrasina DOKUNULMAZ - senaryo bitince
        // oradan kaldigi yerden devam edilebilsin diye (bkz.
        // TryAutoCompleteRunbookAsync). Rejoin noktasi tanimli degilse (tek yonlu
        // dal) TUM acik ana akis gorevleri Atlandi olur - eski davranis.
        //
        // Yalnizca gercekten "acik" (Bekliyor/Devam Eden/Bloke) gorevler Atlandi
        // olur. IsClosed() (Tamamlandi/Atlandi/N-A) burada YETERLI DEGIL - Failed
        // de kapanmis sayilmalidir, aksi halde bu dongu az once "Basarisiz"
        // isaretlenmis (senaryoyu tetikleyen) gorevin durumunu sessizce
        // "Atlandi"ya cevirip asil sebebi gizlerdi.
        var tasksToSkip = mainTasks.Where(t =>
            t.Status is RunbookTaskStatus.NotStarted or RunbookTaskStatus.InProgress or RunbookTaskStatus.Blocked
            && (rejoinOrder is null || t.Order < rejoinOrder.Value));

        foreach (var task in tasksToSkip)
        {
            var oldStatus = task.Status;
            task.Status = RunbookTaskStatus.Skipped;
            task.ActualStart ??= now;
            task.ActualEnd = now;
            AddActivity(task.Id, TaskActivityType.StatusChanged,
                $"Durum {DisplayText.Status(oldStatus)} -> {DisplayText.Status(RunbookTaskStatus.Skipped)} ({reason})",
                DisplayText.Status(oldStatus), DisplayText.Status(RunbookTaskStatus.Skipped));
        }

        // Senaryo adiminin ne zaman tetiklenecegi onceden bilinemedigi icin
        // tarihleri simdi, sirayla (bir onceki adimin bitisine gore) hesaplanir -
        // yalnizca tahmini sureleriyle onceden girilmislerdi.
        var cursor = now;
        foreach (var step in scenarioSteps)
        {
            step.PlannedStart = cursor;
            cursor = step.EstimatedMinutes.HasValue ? cursor.AddMinutes(step.EstimatedMinutes.Value) : cursor;
            step.PlannedEnd = cursor;
        }

        runbook.ActiveScenarioGroup = scenarioGroup;
        if (runbook.PlannedEnd is null || cursor > runbook.PlannedEnd.Value)
        {
            runbook.PlannedEnd = cursor;
        }
    }

    public async Task ReorderAsync(Guid runbookId, ReorderTasksRequest request, CancellationToken ct = default)
    {
        await access.EnsureForRunbookAsync(runbookId, Permissions.TaskWrite, ct);

        var tasks = await db.Tasks.Where(t => t.RunbookId == runbookId).ToListAsync(ct);
        var byId = tasks.ToDictionary(t => t.Id);

        if (request.TaskIdsInOrder.Any(id => !byId.ContainsKey(id)))
        {
            throw new BusinessRuleException("Siralama listesindeki bir gorev bu runbook'ta bulunamadi.");
        }

        // Ana akis, geri donus adimlari ve her senaryo grubu ayri listeler olarak
        // siralanir (bkz. RunbookTask.IsRollbackStep, RunbookTask.ScenarioGroup);
        // surukle-birak yalnizca goruntulenen listenin kendi icindeki gorevleri
        // gonderir, digerlerine dokunmaz.
        var firstTask = request.TaskIdsInOrder.Count > 0 ? byId[request.TaskIdsInOrder[0]] : null;
        var isRollbackGroup = firstTask?.IsRollbackStep ?? false;
        var scenarioGroup = firstTask?.ScenarioGroup;
        var groupCount = tasks.Count(t => t.IsRollbackStep == isRollbackGroup && t.ScenarioGroup == scenarioGroup);

        if (request.TaskIdsInOrder.Count != groupCount ||
            request.TaskIdsInOrder.Any(id => byId[id].IsRollbackStep != isRollbackGroup || byId[id].ScenarioGroup != scenarioGroup))
        {
            throw new BusinessRuleException("Siralama listesi gorev grubuyla birebir eslesmiyor.");
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
    /// Bir senaryo adiminin rejoin noktasinin (ScenarioRejoinTaskId) gecerli
    /// olup olmadigini dogrular: hedef, ayni runbook'ta ve ANA AKISTA (geri
    /// donus adimi degil, baska bir senaryonun parcasi degil) olmalidir -
    /// aksi halde senaryo bitince nereye donulecegi belirsiz/anlamsiz olurdu.
    /// </summary>
    private async Task ValidateScenarioRejoinTaskAsync(Guid runbookId, Guid rejoinTaskId, CancellationToken ct)
    {
        var target = await db.Tasks
            .Where(t => t.Id == rejoinTaskId && t.RunbookId == runbookId)
            .Select(t => new { t.IsRollbackStep, t.ScenarioGroup })
            .FirstOrDefaultAsync(ct);

        if (target is null)
        {
            throw new NotFoundException("Gorev", rejoinTaskId);
        }

        if (target.IsRollbackStep || target.ScenarioGroup is not null)
        {
            throw new BusinessRuleException("Senaryo bitince devam edilecek gorev ana akista olmalidir.");
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

        // Onceden yazilmis ama hic aktive edilmemis geri donus/senaryo adimlari
        // (hala "Baslamadi" durumunda) akisin normal sekilde tamamlanmasini
        // yanlislikla ENGELLEMEMELIDIR - yalnizca su an aktif olan tek grup
        // dikkate alinir: geri donus aktifse SADECE geri donus adimlari (hangi
        // senaryodan tetiklendigi onemsizdir - geri donus listesi senaryolardan
        // bagimsizdir); degilse ana akis (ActiveScenarioGroup bos) veya aktive
        // edilmis senaryonun kendi adimlari.
        List<RunbookTaskStatus> taskStatuses;
        Guid? scenarioRejoinTaskId = null;

        if (runbook.IsRollbackActive)
        {
            taskStatuses = await db.Tasks
                .Where(t => t.RunbookId == runbookId && t.IsRollbackStep)
                .Select(t => t.Status)
                .ToListAsync(ct);
        }
        else if (!string.IsNullOrEmpty(runbook.ActiveScenarioGroup))
        {
            var scenarioTasks = await db.Tasks
                .Where(t => t.RunbookId == runbookId && !t.IsRollbackStep && t.ScenarioGroup == runbook.ActiveScenarioGroup)
                .Select(t => new { t.Status, t.ScenarioRejoinTaskId })
                .ToListAsync(ct);
            taskStatuses = scenarioTasks.Select(t => t.Status).ToList();
            scenarioRejoinTaskId = scenarioTasks.Select(t => t.ScenarioRejoinTaskId).FirstOrDefault(id => id.HasValue);
        }
        else
        {
            taskStatuses = await db.Tasks
                .Where(t => t.RunbookId == runbookId && !t.IsRollbackStep && t.ScenarioGroup == null)
                .Select(t => t.Status)
                .ToListAsync(ct);
        }

        if (taskStatuses.Count == 0 || taskStatuses.Any(status => !status.IsClosed()))
        {
            return;
        }

        if (scenarioRejoinTaskId.HasValue)
        {
            // Senaryo tek yonlu degil: tum adimlari kapandi ama runbook burada
            // bitmiyor, rejoin noktasindan (ve sonrasindan) ana akisa devam
            // ediliyor. Bu adim/sonrasi hic Atlandi yapilmamisti (bkz.
            // ActivateScenario), o yuzden burada baska bir sey yapmaya gerek yok -
            // yalnizca senaryo bayragi kaldirilir ki bir sonraki durum
            // degisikliginde bu metot ana akisi degerlendirsin.
            runbook.ActiveScenarioGroup = null;
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(AuditAction.Update, nameof(Runbook), runbook.Id.ToString(),
                "Senaryo tamamlandigi icin ana akisa geri donuldu.", runbook.Id, ct: ct);
            await realtime.RunbookChangedAsync(runbook.Id, "scenario-rejoined", ct);
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
