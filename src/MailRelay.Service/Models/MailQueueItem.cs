namespace MailRelay.Service.Models;

// dbo.MailQueue satirinin karsiligi.
public sealed class MailQueueItem
{
    public long Id { get; set; }
    public int? ClientApplicationId { get; set; }
    public string? RequestedByUsername { get; set; }
    public string? RequestedByTeam { get; set; }
    public string ToAddresses { get; set; } = "";
    public string? CcAddresses { get; set; }
    public string? BccAddresses { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsBodyHtml { get; set; }

    // Istemcinin bu mail icin gonderdigi opsiyonel gorunen ad override'i (bkz. MailSendRequest.FromDisplayName).
    // Null ise gonderim aninda RelaySettings.FromDisplayName kullanilir.
    public string? FromDisplayNameOverride { get; set; }

    public int Priority { get; set; }
    public string Status { get; set; } = MailStatus.Queued;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? CorrelationId { get; set; }
    public int? SourcePort { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}

public sealed class MailAttachmentRecord
{
    public long Id { get; set; }
    public long MailQueueId { get; set; }
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed class MailLogSearchFilter
{
    public string? SearchText { get; set; }
    public string? Status { get; set; }
    public string? RequestedByUsername { get; set; }
    public string? RequestedByTeam { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
}
