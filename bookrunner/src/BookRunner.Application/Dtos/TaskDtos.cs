using System.ComponentModel.DataAnnotations;
using BookRunner.Domain.Enums;

namespace BookRunner.Application.Dtos;

/// <summary>Runbook detayinda renkli bar olarak cizilen gorev.</summary>
public sealed record RunbookTaskDto
{
    public Guid Id { get; init; }
    public Guid RunbookId { get; init; }
    public int Order { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string ColorHex { get; init; }
    public RunbookTaskStatus Status { get; init; }
    public required string StatusText { get; init; }
    public TaskPriority Priority { get; init; }
    public required string PriorityText { get; init; }
    public int? EstimatedMinutes { get; init; }
    public DateTimeOffset? PlannedStart { get; init; }
    public DateTimeOffset? PlannedEnd { get; init; }
    public DateTimeOffset? ActualStart { get; init; }
    public DateTimeOffset? ActualEnd { get; init; }
    public Guid? DependsOnTaskId { get; init; }
    public string? DependsOnTaskTitle { get; init; }
    public Guid? ScriptId { get; init; }
    public string? ScriptName { get; init; }
    public string? RollbackNotes { get; init; }

    /// <summary>Aktif atamalar (kisi ve/veya grup rozetleri).</summary>
    public IReadOnlyList<TaskAssignmentDto> Assignments { get; init; } = Array.Empty<TaskAssignmentDto>();

    /// <summary>Gorev altinda listelenen yorumlar.</summary>
    public IReadOnlyList<TaskCommentDto> Comments { get; init; } = Array.Empty<TaskCommentDto>();

    public int CommentCount { get; init; }
    public int ActivityCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>Bir goreve yapilan atama (kisi veya AD grubu).</summary>
public sealed record TaskAssignmentDto
{
    public Guid Id { get; init; }
    public AssigneeType AssigneeType { get; init; }
    public PersonSummary? User { get; init; }
    public GroupSummary? Group { get; init; }
    public bool IsActive { get; init; }
    public string? HandoverNote { get; init; }
    public Guid? HandedOverFromAssignmentId { get; init; }
    public DateTimeOffset AssignedAt { get; init; }
    public required string AssignedBy { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }

    /// <summary>Rozet metni: kisi bas harfleri ya da grup kisaltmasi.</summary>
    public string Initials => AssigneeType == AssigneeType.User ? User?.Initials ?? "?" : Group?.Initials ?? "?";

    public string DisplayName => AssigneeType == AssigneeType.User ? User?.DisplayName ?? "?" : Group?.DisplayName ?? Group?.Name ?? "?";

    public string AvatarColor => AssigneeType == AssigneeType.User ? User?.AvatarColor ?? "#888" : Group?.AvatarColor ?? "#888";
}

/// <summary>Gorev yorumu.</summary>
public sealed record TaskCommentDto
{
    public Guid Id { get; init; }
    public Guid TaskId { get; init; }
    public required PersonSummary Author { get; init; }
    public required string Body { get; init; }
    public Guid? ParentCommentId { get; init; }
    public bool IsEdited { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyList<PersonSummary> Mentions { get; init; } = Array.Empty<PersonSummary>();
}

/// <summary>Goreve tiklaninca acilan akordiyondaki tarihce satiri.</summary>
public sealed record TaskActivityDto
{
    public long Id { get; init; }
    public TaskActivityType Type { get; init; }
    public required string TypeText { get; init; }
    public PersonSummary? Actor { get; init; }
    public required string ActorDisplayName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required string Summary { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Gorev olusturma istegi.</summary>
public sealed record CreateTaskRequest
{
    [Required, StringLength(250, MinimumLength = 2)]
    public required string Title { get; init; }

    [StringLength(8000)]
    public string? Description { get; init; }

    /// <summary>Bos birakilirsa sira numarasina gore otomatik renk atanir.</summary>
    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Renk #RRGGBB formatinda olmalidir.")]
    public string? ColorHex { get; init; }

    public TaskPriority Priority { get; init; } = TaskPriority.Normal;

    [Range(0, 100000)]
    public int? EstimatedMinutes { get; init; }

    public DateTimeOffset? PlannedStart { get; init; }

    public DateTimeOffset? PlannedEnd { get; init; }

    public Guid? DependsOnTaskId { get; init; }

    [StringLength(4000)]
    public string? RollbackNotes { get; init; }

    /// <summary>Bos birakilirsa gorev listenin sonuna eklenir.</summary>
    public int? Order { get; init; }
}

/// <summary>Gorev guncelleme istegi.</summary>
public sealed record UpdateTaskRequest
{
    [Required, StringLength(250, MinimumLength = 2)]
    public required string Title { get; init; }

    [StringLength(8000)]
    public string? Description { get; init; }

    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Renk #RRGGBB formatinda olmalidir.")]
    public string? ColorHex { get; init; }

    public TaskPriority Priority { get; init; } = TaskPriority.Normal;

    [Range(0, 100000)]
    public int? EstimatedMinutes { get; init; }

    public DateTimeOffset? PlannedStart { get; init; }

    public DateTimeOffset? PlannedEnd { get; init; }

    public Guid? DependsOnTaskId { get; init; }

    [StringLength(4000)]
    public string? RollbackNotes { get; init; }

    public Guid? ScriptId { get; init; }
}

/// <summary>Gorev durumu degistirme istegi.</summary>
public sealed record ChangeTaskStatusRequest
{
    public RunbookTaskStatus Status { get; init; }

    [StringLength(2000)]
    public string? Note { get; init; }
}

/// <summary>Gorevleri yeniden siralama istegi (surukle-birak).</summary>
public sealed record ReorderTasksRequest
{
    /// <summary>Gorev kimlikleri, istenen yeni sirada.</summary>
    [Required, MinLength(1)]
    public required IReadOnlyList<Guid> TaskIdsInOrder { get; init; }
}

/// <summary>Goreve kisi veya grup atama istegi.</summary>
public sealed record AssignTaskRequest
{
    public AssigneeType AssigneeType { get; init; }

    /// <summary>Kisi atamasi icin AD SID'i; kullanici yerelde yoksa AD'den senkronize edilir.</summary>
    public string? UserSid { get; init; }

    /// <summary>Grup atamasi icin AD SID'i.</summary>
    public string? GroupSid { get; init; }

    /// <summary>Yerelde kayitli kullanici kimligi (SID yerine kullanilabilir).</summary>
    public Guid? UserId { get; init; }

    public Guid? GroupId { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }

    /// <summary>false ise atama bildirimi e-postasi gonderilmez.</summary>
    public bool Notify { get; init; } = true;
}

/// <summary>Gorevi baska bir kisiye/gruba devretme istegi.</summary>
public sealed record HandoverTaskRequest
{
    /// <summary>Devredilen mevcut atama.</summary>
    public Guid FromAssignmentId { get; init; }

    public AssigneeType TargetType { get; init; }

    public string? TargetUserSid { get; init; }

    public string? TargetGroupSid { get; init; }

    public Guid? TargetUserId { get; init; }

    public Guid? TargetGroupId { get; init; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public required string Note { get; init; }
}

/// <summary>Yorum ekleme istegi.</summary>
public sealed record CreateCommentRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public required string Body { get; init; }

    public Guid? ParentCommentId { get; init; }

    /// <summary>Yorumda @ ile anilan kullanicilar.</summary>
    public IReadOnlyList<Guid>? MentionedUserIds { get; init; }
}
