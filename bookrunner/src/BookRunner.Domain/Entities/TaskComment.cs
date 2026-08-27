using BookRunner.Domain.Common;

namespace BookRunner.Domain.Entities;

/// <summary>Gorev altinda listelenen yorum. Yanit vererek zincir olusturulabilir.</summary>
public class TaskComment : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }
    public RunbookTask Task { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public AppUser Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    /// <summary>Yanit verilen yorum (null ise kok yorum).</summary>
    public Guid? ParentCommentId { get; set; }
    public TaskComment? ParentComment { get; set; }

    /// <summary>Yorumda @ ile anilan kullanici kimlikleri; virgulle ayrilmis GUID listesi.</summary>
    public string? MentionedUserIds { get; set; }

    public bool IsEdited { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<TaskComment> Replies { get; set; } = new List<TaskComment>();
}
