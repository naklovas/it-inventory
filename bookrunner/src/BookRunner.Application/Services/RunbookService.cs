using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookRunner.Application.Services;

/// <summary>Runbook ve sablon is kurallari.</summary>
public sealed class RunbookService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRunbookAccess access,
    IAuditService audit,
    IRealtimeNotifier realtime,
    IExternalIntegrationClient integration,
    IGamificationService gamification,
    ILogger<RunbookService> logger) : IRunbookService
{
    public async Task<PagedResult<RunbookListItemDto>> ListAsync(RunbookFilter filter, CancellationToken ct = default)
    {
        var query = db.Runbooks.AsNoTracking().AsQueryable();

        if (filter.IsTemplate.HasValue)
        {
            query = query.Where(r => r.IsTemplate == filter.IsTemplate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(r =>
                EF.Functions.Like(r.Title, term) ||
                EF.Functions.Like(r.Code, term) ||
                (r.Description != null && EF.Functions.Like(r.Description, term)));
        }

        if (filter.Statuses is { Length: > 0 })
        {
            query = query.Where(r => filter.Statuses.Contains(r.Status));
        }

        if (!string.IsNullOrWhiteSpace(filter.TemplateCategory))
        {
            query = query.Where(r => r.TemplateCategory == filter.TemplateCategory);
        }

        if (filter.OwnerUserId.HasValue)
        {
            query = query.Where(r => r.OwnerUserId == filter.OwnerUserId.Value);
        }

        if (filter.AssignedToUserId.HasValue)
        {
            var userId = filter.AssignedToUserId.Value;
            query = query.Where(r => r.Tasks.Any(t =>
                t.Assignments.Any(a => a.IsActive &&
                    (a.UserId == userId ||
                     (a.GroupId != null && a.Group!.Members.Any(m => m.UserId == userId))))));
        }

        if (filter.AssignedToGroupId.HasValue)
        {
            var groupId = filter.AssignedToGroupId.Value;
            query = query.Where(r => r.Tasks.Any(t => t.Assignments.Any(a => a.IsActive && a.GroupId == groupId)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = $"%{filter.Tag.Trim()}%";
            query = query.Where(r => r.Tags != null && EF.Functions.Like(r.Tags, tag));
        }

        if (!string.IsNullOrWhiteSpace(filter.ServiceManagerWorkItemId))
        {
            query = query.Where(r => r.ServiceManagerWorkItemId == filter.ServiceManagerWorkItemId);
        }

        if (filter.PlannedStartFrom.HasValue)
        {
            query = query.Where(r => r.PlannedStart >= filter.PlannedStartFrom.Value);
        }

        if (filter.PlannedStartTo.HasValue)
        {
            query = query.Where(r => r.PlannedStart <= filter.PlannedStartTo.Value);
        }

        query = ApplySort(query, filter.SortBy, filter.SortDescending);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        // Katilimci rozetleri icin once yalnizca kullanici kimlikleri (Guid) cekilir.
        // SQL Server DISTINCT icinde varbinary(max) sutunu (AppUser.Photo) kabul
        // etmez ("Operand data type varbinary(max) is invalid for the DISTINCT
        // operator"), ve EF Core da hesaplanmis bir alanla (orn. Photo != null)
        // birlikte entity projeksiyonunu Distinct + collection-subquery baglaminda
        // ceviremiyor. Bu yuzden Distinct yalnizca saf Guid uzerinde calisir; kisi
        // ozetleri asagida TEK bir ayri sorguyla topluca cozulur.
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                Runbook = r,
                Owner = r.Owner,
                TaskCount = r.Tasks.Count,
                CompletedTaskCount = r.Tasks.Count(t => t.Status == RunbookTaskStatus.Completed || t.Status == RunbookTaskStatus.Skipped),
                CommentCount = r.Tasks.SelectMany(t => t.Comments).Count(c => !c.IsDeleted),
                ParticipantUserIds = r.Tasks
                    .SelectMany(t => t.Assignments)
                    .Where(a => a.IsActive && a.UserId != null)
                    .Select(a => a.UserId!.Value)
                    .Distinct()
                    .Take(5)
                    .ToList()
            })
            .ToListAsync(ct);

        var participantIds = rows.SelectMany(r => r.ParticipantUserIds).Distinct().ToList();
        var participantLookup = new Dictionary<Guid, PersonSummary>();
        if (participantIds.Count > 0)
        {
            // ToSummary() bir C# yardimci metodu oldugu icin SQL'e cevrilemez;
            // once kullanicilar cekilir, esleme bellekte (client-side) yapilir.
            var participantUsers = await db.Users.AsNoTracking()
                .Where(u => participantIds.Contains(u.Id))
                .ToListAsync(ct);

            foreach (var user in participantUsers)
            {
                participantLookup[user.Id] = user.ToSummary();
            }
        }

        var items = rows.Select(row => new RunbookListItemDto
        {
            Id = row.Runbook.Id,
            Code = row.Runbook.Code,
            Title = row.Runbook.Title,
            Description = Truncate(row.Runbook.Description, 220),
            Status = row.Runbook.Status,
            StatusText = DisplayText.Status(row.Runbook.Status),
            IsTemplate = row.Runbook.IsTemplate,
            TemplateCategory = row.Runbook.TemplateCategory,
            PlannedStart = row.Runbook.PlannedStart,
            PlannedEnd = row.Runbook.PlannedEnd,
            Owner = row.Owner?.ToSummary(),
            ServiceManagerWorkItemId = row.Runbook.ServiceManagerWorkItemId,
            Tags = Mapping.SplitTags(row.Runbook.Tags),
            TaskCount = row.TaskCount,
            CompletedTaskCount = row.CompletedTaskCount,
            CommentCount = row.CommentCount,
            Participants = row.ParticipantUserIds
                .Where(participantLookup.ContainsKey)
                .Select(id => participantLookup[id])
                .ToList(),
            CreatedAt = row.Runbook.CreatedAt,
            UpdatedAt = row.Runbook.UpdatedAt
        }).ToList();

        return PagedResult<RunbookListItemDto>.Create(items, page, pageSize, total);
    }

    public async Task<RunbookDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var runbook = await LoadDetailAsync(id, tracking: false, ct)
            ?? throw new NotFoundException("Runbook", id);

        var mentionLookup = await BuildMentionLookupAsync(runbook, ct);

        var tasks = runbook.Tasks
            .OrderBy(t => t.Order)
            .Select(t => t.ToDto(includeComments: true, mentionLookup))
            .ToList();

        return runbook.ToDetailDto(tasks);
    }

    public async Task<RunbookDetailDto> CreateAsync(CreateRunbookRequest request, CancellationToken ct = default)
    {
        access.Ensure(Permissions.RunbookWrite);
        ValidatePlannedRange(request.PlannedStart, request.PlannedEnd);

        var ownerId = request.OwnerUserId ?? currentUser.UserId
            ?? throw new BusinessRuleException("Runbook sahibi belirlenemedi. Kullanici Active Directory'den senkronize edilmemis olabilir.");

        if (!await db.Users.AnyAsync(u => u.Id == ownerId, ct))
        {
            throw new NotFoundException("Kullanici", ownerId);
        }

        var runbook = new Runbook
        {
            Code = await GenerateCodeAsync(request.IsTemplate, ct),
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = RunbookStatus.Draft,
            IsTemplate = request.IsTemplate,
            TemplateCategory = request.TemplateCategory,
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            OwnerUserId = ownerId,
            ServiceManagerWorkItemId = request.ServiceManagerWorkItemId,
            Tags = Mapping.JoinTags(request.Tags)
        };

        db.Runbooks.Add(runbook);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Create, nameof(Runbook), runbook.Id.ToString(),
            $"{runbook.Code} runbook'u olusturuldu.", runbook.Id, ct: ct);

        logger.LogInformation("Runbook {Code} olusturuldu ({User}).", runbook.Code, currentUser.UserName);

        return await GetAsync(runbook.Id, ct);
    }

    public async Task<RunbookDetailDto> UpdateAsync(Guid id, UpdateRunbookRequest request, CancellationToken ct = default)
    {
        // Runbook'un sahibi, rol izni olmasa da kendi runbook'unu duzenleyebilir.
        await access.EnsureForRunbookAsync(id, Permissions.RunbookWrite, ct);
        ValidatePlannedRange(request.PlannedStart, request.PlannedEnd);

        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Runbook", id);

        if (!string.IsNullOrWhiteSpace(request.RowVersion) && runbook.RowVersion is not null)
        {
            var incoming = Convert.FromBase64String(request.RowVersion);
            if (!incoming.SequenceEqual(runbook.RowVersion))
            {
                throw new BusinessRuleException(
                    "Bu runbook siz duzenlerken baskasi tarafindan degistirildi. Sayfayi yenileyip tekrar deneyin.");
            }
        }

        var oldStatus = runbook.Status;

        runbook.Title = request.Title.Trim();
        runbook.Description = request.Description;
        runbook.TemplateCategory = request.TemplateCategory;
        runbook.PlannedStart = request.PlannedStart;
        runbook.PlannedEnd = request.PlannedEnd;
        runbook.ServiceManagerWorkItemId = request.ServiceManagerWorkItemId;
        runbook.Tags = Mapping.JoinTags(request.Tags);

        if (request.OwnerUserId.HasValue && request.OwnerUserId.Value != runbook.OwnerUserId)
        {
            if (!await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
            {
                throw new NotFoundException("Kullanici", request.OwnerUserId.Value);
            }

            runbook.OwnerUserId = request.OwnerUserId.Value;
        }

        if (request.Status != oldStatus)
        {
            ApplyStatusTransition(runbook, request.Status);
        }

        await db.SaveChangesAsync(ct);

        if (request.Status == RunbookStatus.Completed && oldStatus != RunbookStatus.Completed)
        {
            await gamification.OnRunbookCompletedAsync(runbook, ct);
        }

        await audit.LogAsync(AuditAction.Update, nameof(Runbook), runbook.Id.ToString(),
            $"{runbook.Code} runbook'u guncellendi.", runbook.Id, ct: ct);

        await realtime.RunbookChangedAsync(runbook.Id, "updated", ct);

        if (request.Status != oldStatus)
        {
            await PublishExternalAsync(runbook, "runbook.status-changed",
                $"Durum {DisplayText.Status(oldStatus)} -> {DisplayText.Status(runbook.Status)}", ct);
        }

        return await GetAsync(runbook.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Silme yetkisi yonetici rolunde; runbook sahibi de kendi runbook'unu silebilir.
        await access.EnsureForRunbookAsync(id, Permissions.RunbookDelete, ct);

        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Runbook", id);

        if (runbook.Status == RunbookStatus.InProgress)
        {
            throw new BusinessRuleException("Devam eden bir runbook silinemez. Once iptal edin veya tamamlayin.");
        }

        // Silme mantiksaldir: gecmis gorev/yorum/audit kayitlari korunur.
        runbook.IsDeleted = true;
        runbook.DeletedAt = DateTimeOffset.UtcNow;
        runbook.DeletedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Delete, nameof(Runbook), runbook.Id.ToString(),
            $"{runbook.Code} runbook'u silindi.", runbook.Id, ct: ct);
    }

    public async Task<RunbookDetailDto> SaveAsTemplateAsync(
        Guid runbookId, string templateTitle, string? category, CancellationToken ct = default)
    {
        await access.EnsureForRunbookAsync(runbookId, Permissions.RunbookPublishTemplate, ct);

        var source = await LoadDetailAsync(runbookId, tracking: false, ct)
            ?? throw new NotFoundException("Runbook", runbookId);

        var ownerId = currentUser.UserId ?? source.OwnerUserId;

        var template = new Runbook
        {
            Code = await GenerateCodeAsync(isTemplate: true, ct),
            Title = string.IsNullOrWhiteSpace(templateTitle) ? $"{source.Title} (Sablon)" : templateTitle.Trim(),
            Description = source.Description,
            Status = RunbookStatus.Draft,
            IsTemplate = true,
            TemplateCategory = category ?? source.TemplateCategory,
            OwnerUserId = ownerId,
            Tags = source.Tags
        };

        foreach (var task in source.Tasks.OrderBy(t => t.Order))
        {
            template.Tasks.Add(CopyTask(task, copyAssignments: false));
        }

        db.Runbooks.Add(template);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Create, nameof(Runbook), template.Id.ToString(),
            $"{source.Code} runbook'undan {template.Code} sablonu olusturuldu.", template.Id, ct: ct);

        return await GetAsync(template.Id, ct);
    }

    public async Task<RunbookDetailDto> CreateFromTemplateAsync(
        Guid templateId, CreateFromTemplateRequest request, CancellationToken ct = default)
    {
        await access.EnsureForRunbookAsync(templateId, Permissions.RunbookWrite, ct);
        ValidatePlannedRange(request.PlannedStart, request.PlannedEnd);

        var template = await LoadDetailAsync(templateId, tracking: false, ct)
            ?? throw new NotFoundException("Sablon", templateId);

        if (!template.IsTemplate)
        {
            throw new BusinessRuleException("Kaynak kayit bir sablon degil.");
        }

        var ownerId = request.OwnerUserId ?? currentUser.UserId ?? template.OwnerUserId;

        var runbook = new Runbook
        {
            Code = await GenerateCodeAsync(isTemplate: false, ct),
            Title = request.Title.Trim(),
            Description = template.Description,
            Status = RunbookStatus.Draft,
            IsTemplate = false,
            SourceTemplateId = template.Id,
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            OwnerUserId = ownerId,
            ServiceManagerWorkItemId = request.ServiceManagerWorkItemId,
            Tags = template.Tags
        };

        foreach (var task in template.Tasks.OrderBy(t => t.Order))
        {
            runbook.Tasks.Add(CopyTask(task, request.CopyAssignments));
        }

        db.Runbooks.Add(runbook);
        await db.SaveChangesAsync(ct);

        foreach (var task in runbook.Tasks)
        {
            db.Activities.Add(new TaskActivity
            {
                TaskId = task.Id,
                Type = TaskActivityType.Created,
                ActorUserId = currentUser.UserId,
                ActorDisplayName = currentUser.DisplayName,
                Summary = $"Gorev '{template.Code}' sablonundan olusturuldu."
            });
        }

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Create, nameof(Runbook), runbook.Id.ToString(),
            $"{template.Code} sablonundan {runbook.Code} runbook'u uretildi.", runbook.Id, ct: ct);

        return await GetAsync(runbook.Id, ct);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;

        var groupIds = userId.HasValue
            ? await db.UserGroups.Where(ug => ug.UserId == userId.Value).Select(ug => ug.GroupId).ToListAsync(ct)
            : [];

        var openStatuses = new[] { RunbookTaskStatus.NotStarted, RunbookTaskStatus.InProgress, RunbookTaskStatus.Blocked };

        var activeRunbooks = await db.Runbooks.CountAsync(r => !r.IsTemplate && r.Status == RunbookStatus.InProgress, ct);
        var draftRunbooks = await db.Runbooks.CountAsync(r => !r.IsTemplate && r.Status == RunbookStatus.Draft, ct);
        var templateCount = await db.Runbooks.CountAsync(r => r.IsTemplate, ct);

        var myTaskQuery = db.Tasks
            .AsNoTracking()
            .Where(t => openStatuses.Contains(t.Status) && !t.Runbook.IsTemplate)
            .Where(t => t.Assignments.Any(a => a.IsActive &&
                        ((userId.HasValue && a.UserId == userId.Value) ||
                         (a.GroupId != null && groupIds.Contains(a.GroupId.Value)))));

        var myTasks = await myTaskQuery
            .OrderBy(t => t.PlannedStart ?? DateTimeOffset.MaxValue)
            .ThenByDescending(t => t.Priority)
            .Take(20)
            .Select(t => new
            {
                t.Id,
                t.RunbookId,
                RunbookCode = t.Runbook.Code,
                RunbookTitle = t.Runbook.Title,
                t.Title,
                t.ColorHex,
                t.Status,
                t.Priority,
                t.PlannedStart,
                DirectlyAssigned = userId.HasValue && t.Assignments.Any(a => a.IsActive && a.UserId == userId.Value),
                GroupName = t.Assignments
                    .Where(a => a.IsActive && a.GroupId != null && groupIds.Contains(a.GroupId!.Value))
                    .Select(a => a.Group!.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var recent = await ListAsync(new RunbookFilter { IsTemplate = false, PageSize = 6, SortBy = "updatedAt" }, ct);

        return new DashboardDto
        {
            ActiveRunbooks = activeRunbooks,
            DraftRunbooks = draftRunbooks,
            TemplateCount = templateCount,
            MyOpenTasks = userId.HasValue
                ? await db.Tasks.CountAsync(t => openStatuses.Contains(t.Status) && !t.Runbook.IsTemplate &&
                    t.Assignments.Any(a => a.IsActive && a.UserId == userId.Value), ct)
                : 0,
            MyTeamsOpenTasks = groupIds.Count == 0
                ? 0
                : await db.Tasks.CountAsync(t => openStatuses.Contains(t.Status) && !t.Runbook.IsTemplate &&
                    t.Assignments.Any(a => a.IsActive && a.GroupId != null && groupIds.Contains(a.GroupId.Value)), ct),
            BlockedTasks = await db.Tasks.CountAsync(t => t.Status == RunbookTaskStatus.Blocked && !t.Runbook.IsTemplate, ct),
            RecentRunbooks = recent.Items,
            MyTasks = myTasks.Select(t => new MyTaskDto
            {
                TaskId = t.Id,
                RunbookId = t.RunbookId,
                RunbookCode = t.RunbookCode,
                RunbookTitle = t.RunbookTitle,
                Title = t.Title,
                ColorHex = t.ColorHex,
                Status = t.Status,
                StatusText = DisplayText.Status(t.Status),
                Priority = t.Priority,
                PlannedStart = t.PlannedStart,
                AssignedVia = t.DirectlyAssigned ? "Dogrudan" : t.GroupName ?? "Grup"
            }).ToList()
        };
    }

    /// <summary>Detay ekrani icin runbook'u tum iliskileriyle yukler.</summary>
    private async Task<Runbook?> LoadDetailAsync(Guid id, bool tracking, CancellationToken ct)
    {
        var query = db.Runbooks
            .Include(r => r.Owner)
            .Include(r => r.SourceTemplate)
            .Include(r => r.Tasks).ThenInclude(t => t.Assignments).ThenInclude(a => a.User)
            .Include(r => r.Tasks).ThenInclude(t => t.Assignments).ThenInclude(a => a.Group)
            .Include(r => r.Tasks).ThenInclude(t => t.Comments).ThenInclude(c => c.Author)
            .Include(r => r.Tasks).ThenInclude(t => t.Activities)
            .Include(r => r.Tasks).ThenInclude(t => t.Script)
            .Include(r => r.Tasks).ThenInclude(t => t.DependsOnTask)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    /// <summary>Yorumlarda anilan kullanicilari tek sorguda cozer.</summary>
    private async Task<IReadOnlyDictionary<Guid, AppUser>> BuildMentionLookupAsync(Runbook runbook, CancellationToken ct)
    {
        var ids = runbook.Tasks
            .SelectMany(t => t.Comments)
            .Where(c => !string.IsNullOrWhiteSpace(c.MentionedUserIds))
            .SelectMany(c => c.MentionedUserIds!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(raw => Guid.TryParse(raw, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, AppUser>();
        }

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
    }

    /// <summary>Sablon/runbook kopyalarken gorev alanlarini tasir.</summary>
    private static RunbookTask CopyTask(RunbookTask source, bool copyAssignments)
    {
        var copy = new RunbookTask
        {
            Order = source.Order,
            Title = source.Title,
            Description = source.Description,
            ColorHex = source.ColorHex,
            Status = RunbookTaskStatus.NotStarted,
            Priority = source.Priority,
            EstimatedMinutes = source.EstimatedMinutes,
            RollbackNotes = source.RollbackNotes
        };

        if (copyAssignments)
        {
            foreach (var assignment in source.Assignments.Where(a => a.IsActive))
            {
                copy.Assignments.Add(new TaskAssignment
                {
                    AssigneeType = assignment.AssigneeType,
                    UserId = assignment.UserId,
                    GroupId = assignment.GroupId,
                    IsActive = true
                });
            }
        }

        return copy;
    }

    /// <summary>Durum gecisinde gercek baslangic/bitis zamanlarini isler.</summary>
    private static void ApplyStatusTransition(Runbook runbook, RunbookStatus newStatus)
    {
        var now = DateTimeOffset.UtcNow;

        switch (newStatus)
        {
            case RunbookStatus.InProgress:
                runbook.ActualStart ??= now;
                runbook.ActualEnd = null;
                break;
            case RunbookStatus.Completed:
                runbook.ActualStart ??= now;
                runbook.ActualEnd = now;
                break;
            case RunbookStatus.Cancelled:
                runbook.ActualEnd = now;
                break;
        }

        runbook.Status = newStatus;
    }

    private async Task PublishExternalAsync(Runbook runbook, string eventType, string message, CancellationToken ct)
    {
        if (!integration.IsEnabled)
        {
            return;
        }

        await integration.PublishEventAsync(new ExternalEvent
        {
            EventType = eventType,
            RunbookId = runbook.Id,
            RunbookCode = runbook.Code,
            RunbookTitle = runbook.Title,
            Status = DisplayText.Status(runbook.Status),
            ActorDisplayName = currentUser.DisplayName,
            Message = message
        }, ct);
    }

    /// <summary>"RB-2026-0042" / "TPL-2026-0007" formatinda sonraki kodu uretir.</summary>
    private async Task<string> GenerateCodeAsync(bool isTemplate, CancellationToken ct)
    {
        var prefix = isTemplate ? "TPL" : "RB";
        var year = DateTimeOffset.UtcNow.Year;
        var pattern = $"{prefix}-{year}-";

        var lastCode = await db.Runbooks
            .IgnoreQueryFilters()
            .Where(r => r.Code.StartsWith(pattern))
            .OrderByDescending(r => r.Code)
            .Select(r => r.Code)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (lastCode is not null && int.TryParse(lastCode[pattern.Length..], out var lastNumber))
        {
            next = lastNumber + 1;
        }

        return $"{pattern}{next:D4}";
    }

    private static IQueryable<Runbook> ApplySort(IQueryable<Runbook> query, string? sortBy, bool descending)
        => (sortBy?.ToLowerInvariant()) switch
        {
            "code" => descending ? query.OrderByDescending(r => r.Code) : query.OrderBy(r => r.Code),
            "title" => descending ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "plannedstart" => descending ? query.OrderByDescending(r => r.PlannedStart) : query.OrderBy(r => r.PlannedStart),
            "createdat" => descending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
            _ => descending
                ? query.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                : query.OrderBy(r => r.UpdatedAt ?? r.CreatedAt)
        };

    private static void ValidatePlannedRange(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            throw ValidationException.Single(nameof(CreateRunbookRequest.PlannedEnd),
                "Planlanan bitis, planlanan baslangictan once olamaz.");
        }
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength] + "...";
}
