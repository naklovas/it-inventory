using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;

namespace BookRunner.Application.Abstractions;

/// <summary>AD kayitlarini yerel projeksiyona senkronize eder.</summary>
public interface IDirectorySyncService
{
    /// <summary>SID ile kullaniciyi bulur; yoksa AD'den okuyup olusturur.</summary>
    Task<AppUser> EnsureUserBySidAsync(string sid, CancellationToken ct = default);

    /// <summary>Oturum adiyla kullaniciyi bulur; yoksa AD'den okuyup olusturur.</summary>
    Task<AppUser?> EnsureUserBySamAccountNameAsync(string samAccountName, CancellationToken ct = default);

    Task<AppGroup> EnsureGroupBySidAsync(string sid, CancellationToken ct = default);

    /// <summary>Kullanicinin AD grup uyeliklerini yerel tabloya yansitir.</summary>
    Task SyncUserGroupsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Once yerelde, yeterli sonuc yoksa AD'de kullanici arar.</summary>
    Task<IReadOnlyList<PersonSummary>> SearchUsersAsync(string term, int take, CancellationToken ct = default);

    Task<IReadOnlyList<GroupSummary>> SearchGroupsAsync(string term, int take, CancellationToken ct = default);

    /// <summary>Kullanicinin fotografini dondurur; yerelde yoksa AD'den ceker ve onbellege alir.</summary>
    Task<(byte[] Content, string ContentType)?> GetUserPhotoAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Grubun uyelerini (yerel projeksiyon + AD) dondurur.</summary>
    Task<IReadOnlyList<PersonSummary>> GetGroupMembersAsync(Guid groupId, CancellationToken ct = default);
}

/// <summary>Runbook ve sablon islemleri.</summary>
public interface IRunbookService
{
    Task<PagedResult<RunbookListItemDto>> ListAsync(RunbookFilter filter, CancellationToken ct = default);

    Task<RunbookDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<RunbookDetailDto> CreateAsync(CreateRunbookRequest request, CancellationToken ct = default);

    Task<RunbookDetailDto> UpdateAsync(Guid id, UpdateRunbookRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Mevcut runbook'u (gorevleriyle birlikte) yeni bir sablona kopyalar.</summary>
    Task<RunbookDetailDto> SaveAsTemplateAsync(Guid runbookId, string templateTitle, string? category, CancellationToken ct = default);

    /// <summary>Sablondan yeni bir calisir runbook uretir.</summary>
    Task<RunbookDetailDto> CreateFromTemplateAsync(Guid templateId, CreateFromTemplateRequest request, CancellationToken ct = default);

    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
}

/// <summary>Gorev islemleri.</summary>
public interface ITaskService
{
    Task<RunbookTaskDto> GetAsync(Guid taskId, CancellationToken ct = default);

    Task<RunbookTaskDto> CreateAsync(Guid runbookId, CreateTaskRequest request, CancellationToken ct = default);

    Task<RunbookTaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default);

    Task<RunbookTaskDto> ChangeStatusAsync(Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default);

    Task ReorderAsync(Guid runbookId, ReorderTasksRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Goreve tiklaninca acilan akordiyon tarihcesi.</summary>
    Task<IReadOnlyList<TaskActivityDto>> GetHistoryAsync(Guid taskId, CancellationToken ct = default);
}

/// <summary>Gorev atama ve devir islemleri.</summary>
public interface IAssignmentService
{
    Task<TaskAssignmentDto> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct = default);

    Task<TaskAssignmentDto> HandoverAsync(Guid taskId, HandoverTaskRequest request, CancellationToken ct = default);

    Task RemoveAsync(Guid taskId, Guid assignmentId, CancellationToken ct = default);

    /// <summary>Gecmis atamalar dahil tum atama zinciri.</summary>
    Task<IReadOnlyList<TaskAssignmentDto>> ListAsync(Guid taskId, bool includeInactive, CancellationToken ct = default);
}

/// <summary>Gorev yorumlari.</summary>
public interface ICommentService
{
    Task<IReadOnlyList<TaskCommentDto>> ListAsync(Guid taskId, CancellationToken ct = default);

    Task<TaskCommentDto> AddAsync(Guid taskId, CreateCommentRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid commentId, CancellationToken ct = default);
}

/// <summary>Audit trail sorgulama.</summary>
public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> ListAsync(AuditFilter filter, CancellationToken ct = default);
}

/// <summary>CSX script yonetimi ve calistirma.</summary>
public interface IScriptService
{
    Task<IReadOnlyList<ScriptDto>> ListAsync(Guid? runbookId, CancellationToken ct = default);

    Task<ScriptDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<ScriptDto> SaveAsync(Guid? id, SaveScriptRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<ScriptRunResult> RunAsync(Guid id, RunScriptRequest request, CancellationToken ct = default);
}
