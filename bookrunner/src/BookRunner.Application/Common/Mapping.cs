using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;

namespace BookRunner.Application.Common;

/// <summary>
/// Varlik -> DTO donusumleri. Ek bir esleme kutuphanesi yerine acik yazilmis
/// uzanti metotlari kullanilir; boylece uretilen SQL ve alan secimi gorunur kalir.
/// </summary>
public static class Mapping
{
    public static PersonSummary ToSummary(this AppUser user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Title = user.Title,
        Department = user.Department,
        Initials = string.IsNullOrWhiteSpace(user.Initials) ? AvatarHelper.Initials(user.DisplayName) : user.Initials,
        AvatarColor = user.AvatarColor,
        HasPhoto = user.Photo != null && user.Photo.Length > 0,
        PhotoUrl = user.Photo != null && user.Photo.Length > 0 ? $"/api/directory/users/{user.Id}/photo" : null
    };

    public static GroupSummary ToSummary(this AppGroup group, int memberCount = 0) => new()
    {
        Id = group.Id,
        Name = group.Name,
        DisplayName = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Name : group.DisplayName,
        Initials = AvatarHelper.Initials(string.IsNullOrWhiteSpace(group.DisplayName) ? group.Name : group.DisplayName),
        AvatarColor = group.AvatarColor,
        MemberCount = memberCount
    };

    public static TaskAssignmentDto ToDto(this TaskAssignment assignment) => new()
    {
        Id = assignment.Id,
        AssigneeType = assignment.AssigneeType,
        User = assignment.User?.ToSummary(),
        Group = assignment.Group?.ToSummary(),
        IsActive = assignment.IsActive,
        HandoverNote = assignment.HandoverNote,
        HandedOverFromAssignmentId = assignment.HandedOverFromAssignmentId,
        AssignedAt = assignment.CreatedAt,
        AssignedBy = assignment.CreatedBy,
        ReleasedAt = assignment.ReleasedAt
    };

    public static TaskCommentDto ToDto(this TaskComment comment, IReadOnlyDictionary<Guid, AppUser>? mentionLookup = null)
    {
        var mentions = new List<PersonSummary>();
        if (mentionLookup is not null && !string.IsNullOrWhiteSpace(comment.MentionedUserIds))
        {
            foreach (var raw in comment.MentionedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(raw, out var id) && mentionLookup.TryGetValue(id, out var user))
                {
                    mentions.Add(user.ToSummary());
                }
            }
        }

        return new TaskCommentDto
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            Author = comment.Author.ToSummary(),
            Body = comment.Body,
            ParentCommentId = comment.ParentCommentId,
            IsEdited = comment.IsEdited,
            CreatedAt = comment.CreatedAt,
            Mentions = mentions
        };
    }

    public static TaskActivityDto ToDto(this TaskActivity activity) => new()
    {
        Id = activity.Id,
        Type = activity.Type,
        TypeText = DisplayText.Activity(activity.Type),
        Actor = activity.Actor?.ToSummary(),
        ActorDisplayName = activity.ActorDisplayName,
        OldValue = activity.OldValue,
        NewValue = activity.NewValue,
        Summary = activity.Summary,
        CreatedAt = activity.CreatedAt
    };

    /// <summary>
    /// Gorevi DTO'ya cevirir. <paramref name="includeComments"/> false ise yorumlar
    /// yalnizca sayilir; liste ekranlarinda gereksiz veri tasinmaz.
    /// </summary>
    public static RunbookTaskDto ToDto(
        this RunbookTask task,
        bool includeComments = true,
        IReadOnlyDictionary<Guid, AppUser>? mentionLookup = null) => new()
    {
        Id = task.Id,
        RunbookId = task.RunbookId,
        Order = task.Order,
        Title = task.Title,
        Description = task.Description,
        ColorHex = task.ColorHex,
        Status = task.Status,
        StatusText = DisplayText.Status(task.Status),
        Priority = task.Priority,
        PriorityText = DisplayText.Priority(task.Priority),
        EstimatedMinutes = task.EstimatedMinutes,
        PlannedStart = task.PlannedStart,
        PlannedEnd = task.PlannedEnd,
        ActualStart = task.ActualStart,
        ActualEnd = task.ActualEnd,
        Predecessors = task.Predecessors
            .OrderBy(d => d.DependsOnTask.Order)
            .Select(d => new TaskDependencyRefDto
            {
                TaskId = d.DependsOnTaskId,
                Title = d.DependsOnTask.Title,
                Status = d.DependsOnTask.Status,
                StatusText = DisplayText.Status(d.DependsOnTask.Status)
            })
            .ToList(),
        Successors = task.Successors
            .OrderBy(d => d.Task.Order)
            .Select(d => new TaskDependencyRefDto
            {
                TaskId = d.TaskId,
                Title = d.Task.Title,
                Status = d.Task.Status,
                StatusText = DisplayText.Status(d.Task.Status)
            })
            .ToList(),
        HasOpenPredecessors = task.Predecessors.Any(d => !d.DependsOnTask.Status.IsClosed()),
        ScriptId = task.ScriptId,
        ScriptName = task.Script?.Name,
        RollbackNotes = task.RollbackNotes,
        Assignments = task.Assignments
            .Where(a => a.IsActive)
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.ToDto())
            .ToList(),
        Comments = includeComments
            ? task.Comments
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .Select(c => c.ToDto(mentionLookup))
                .ToList()
            : Array.Empty<TaskCommentDto>(),
        CommentCount = task.Comments.Count(c => !c.IsDeleted),
        ActivityCount = task.Activities.Count,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt
    };

    public static RunbookDetailDto ToDetailDto(
        this Runbook runbook,
        IReadOnlyList<RunbookTaskDto> tasks,
        IReadOnlyList<RunbookCollaboratorDto>? collaborators = null) => new()
    {
        Id = runbook.Id,
        Code = runbook.Code,
        Title = runbook.Title,
        Description = runbook.Description,
        Status = runbook.Status,
        StatusText = DisplayText.Status(runbook.Status),
        IsTemplate = runbook.IsTemplate,
        TemplateCategory = runbook.TemplateCategory,
        SeyirName = runbook.SeyirName,
        SourceTemplateId = runbook.SourceTemplateId,
        SourceTemplateTitle = runbook.SourceTemplate?.Title,
        PlannedStart = runbook.PlannedStart,
        PlannedEnd = runbook.PlannedEnd,
        ActualStart = runbook.ActualStart,
        ActualEnd = runbook.ActualEnd,
        Owner = runbook.Owner?.ToSummary(),
        ServiceManagerWorkItemId = runbook.ServiceManagerWorkItemId,
        Tags = SplitTags(runbook.Tags),
        Tasks = tasks,
        Collaborators = collaborators ?? [],
        CreatedAt = runbook.CreatedAt,
        CreatedBy = runbook.CreatedBy,
        UpdatedAt = runbook.UpdatedAt,
        UpdatedBy = runbook.UpdatedBy,
        RowVersion = runbook.RowVersion is null ? null : Convert.ToBase64String(runbook.RowVersion)
    };

    public static AuditLogDto ToDto(this AuditLog log) => new()
    {
        Id = log.Id,
        Timestamp = log.Timestamp,
        UserName = log.UserName,
        UserDisplayName = log.UserDisplayName,
        Action = log.Action,
        ActionText = DisplayText.Action(log.Action),
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        RunbookId = log.RunbookId,
        Summary = log.Summary,
        Changes = log.Changes,
        IpAddress = log.IpAddress,
        CorrelationId = log.CorrelationId
    };

    public static RoleMappingDto ToDto(this RoleMapping mapping) => new()
    {
        Id = mapping.Id,
        TeamName = mapping.TeamName,
        Role = mapping.Role,
        RoleText = DisplayText.Role(mapping.Role),
        IsActive = mapping.IsActive,
        CreatedAt = mapping.CreatedAt
    };

    public static EmailOutboxDto ToDto(this EmailOutboxMessage message) => new()
    {
        Id = message.Id,
        To = message.To,
        Cc = message.Cc,
        Subject = message.Subject,
        HtmlBody = message.HtmlBody,
        Status = message.Status,
        StatusText = DisplayText.EmailStatus(message.Status),
        AttemptCount = message.AttemptCount,
        CreatedAt = message.CreatedAt,
        SentAt = message.SentAt,
        NextAttemptAt = message.NextAttemptAt,
        LastError = message.LastError,
        Reason = message.Reason,
        RunbookId = message.RunbookId,
        TaskId = message.TaskId
    };

    public static ScriptDto ToDto(this RunbookScript script) => new()
    {
        Id = script.Id,
        RunbookId = script.RunbookId,
        Name = script.Name,
        Description = script.Description,
        Code = script.Code,
        TimeoutSeconds = script.TimeoutSeconds,
        IsEnabled = script.IsEnabled,
        CreatedAt = script.CreatedAt,
        CreatedBy = script.CreatedBy
    };

    public static IReadOnlyList<string> SplitTags(string? tags)
        => string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string? JoinTags(IReadOnlyList<string>? tags)
        => tags is null || tags.Count == 0
            ? null
            : string.Join(',', tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>Sablonun aciklamasi icin gorev durumunun bar dolgu orani.</summary>
    public static bool IsClosed(this RunbookTaskStatus status)
        => status is RunbookTaskStatus.Completed or RunbookTaskStatus.Skipped;
}
