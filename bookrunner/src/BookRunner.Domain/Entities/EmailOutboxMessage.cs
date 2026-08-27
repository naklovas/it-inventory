using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Giden e-posta kuyrugu. E-postalar istek islenirken degil, arka plan servisi
/// tarafindan gonderilir; boylece SMTP arizasi kullanici islemini bozmaz ve
/// hangi bildirimin gittigi denetlenebilir kalir.
/// </summary>
public class EmailOutboxMessage
{
    public long Id { get; set; }

    /// <summary>Noktali virgulle ayrilmis alici adresleri.</summary>
    public string To { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    /// <summary>Bildirimin kaynagi, orn. "TaskAssigned".</summary>
    public string? Reason { get; set; }

    public Guid? RunbookId { get; set; }

    public Guid? TaskId { get; set; }
}
