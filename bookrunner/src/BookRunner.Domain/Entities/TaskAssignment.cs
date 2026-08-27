using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Bir gorevin kisiye veya AD grubuna atanmasi. Devir (handover) sirasinda eski
/// atama pasife cekilir, yenisi eklenir; boylece devir zinciri kaybolmaz.
/// </summary>
public class TaskAssignment : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }
    public RunbookTask Task { get; set; } = null!;

    public AssigneeType AssigneeType { get; set; }

    /// <summary><see cref="AssigneeType"/> = User ise dolu.</summary>
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary><see cref="AssigneeType"/> = Group ise dolu.</summary>
    public Guid? GroupId { get; set; }
    public AppGroup? Group { get; set; }

    /// <summary>false ise atama devredilmis veya kaldirilmistir (tarihce icin saklanir).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Bu atama bir devirle olustuysa devreden atamanin kimligi.</summary>
    public Guid? HandedOverFromAssignmentId { get; set; }

    /// <summary>Devir gerekcesi.</summary>
    public string? HandoverNote { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    /// <summary>Atama bildirimi e-postasinin gonderildigi an.</summary>
    public DateTimeOffset? NotifiedAt { get; set; }
}
