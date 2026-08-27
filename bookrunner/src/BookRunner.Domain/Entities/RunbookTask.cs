using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Runbook icindeki tek bir adim. Arayuzde her gorev kendi rengiyle bir bar olarak
/// cizilir; barin yaninda atanan kisi/grup rozetleri, altinda yorumlar,
/// tiklaninca da akordiyon icinde tarihce gosterilir.
/// </summary>
public class RunbookTask : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunbookId { get; set; }
    public Runbook Runbook { get; set; } = null!;

    /// <summary>Runbook icindeki 1'den baslayan sira numarasi.</summary>
    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Gorev barinin rengi (#RRGGBB). Bos birakilirsa sira numarasina gore atanir.</summary>
    public string ColorHex { get; set; } = "#4F86F7";

    public RunbookTaskStatus Status { get; set; } = RunbookTaskStatus.NotStarted;

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    /// <summary>Planlanan sure (dakika). Zaman cizelgesi barinin genisligini belirler.</summary>
    public int? EstimatedMinutes { get; set; }

    public DateTimeOffset? PlannedStart { get; set; }
    public DateTimeOffset? PlannedEnd { get; set; }
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }

    /// <summary>Bu gorev baslamadan once tamamlanmasi gereken gorev.</summary>
    public Guid? DependsOnTaskId { get; set; }
    public RunbookTask? DependsOnTask { get; set; }

    /// <summary>Gorev icin calistirilabilir CSX script'i (opsiyonel).</summary>
    public Guid? ScriptId { get; set; }
    public RunbookScript? Script { get; set; }

    /// <summary>Geri alma (rollback) adimlarinin aciklamasi.</summary>
    public string? RollbackNotes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskActivity> Activities { get; set; } = new List<TaskActivity>();
}
