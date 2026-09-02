using System.ComponentModel.DataAnnotations;
using BookRunner.Domain.Enums;

namespace BookRunner.Application.Dtos;

/// <summary>Gonderilecek e-posta.</summary>
public sealed record EmailMessage
{
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? Reason { get; init; }
    public Guid? RunbookId { get; init; }
    public Guid? TaskId { get; init; }
}

/// <summary>Excel ice aktarim sonucu.</summary>
public sealed record ImportResult
{
    public int TotalRows { get; init; }
    public int ImportedRows { get; init; }
    public int SkippedRows { get; init; }
    public bool Committed { get; init; }
    public IReadOnlyList<ImportError> Errors { get; init; } = Array.Empty<ImportError>();
    public bool IsSuccess => Errors.Count == 0;
}

/// <summary>Ice aktarimda tek bir satirin hatasi.</summary>
public sealed record ImportError(int Row, string Column, string Message);

/// <summary>Service Manager'dan okunan is kaydi.</summary>
public sealed record ServiceManagerWorkItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? AssignedTo { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public DateTimeOffset? ScheduledStartDate { get; init; }
    public DateTimeOffset? ScheduledEndDate { get; init; }
    public string? WorkItemType { get; init; }
}

/// <summary>Service Manager baglantisinin durumu.</summary>
public sealed record ServiceManagerHealth
{
    public bool IsEnabled { get; init; }
    public bool IsReachable { get; init; }
    public string? Database { get; init; }
    public string? Server { get; init; }
    public long ElapsedMs { get; init; }
    public string? Error { get; init; }
}

/// <summary>CSX script'ine gecirilen calisma baglami.</summary>
public sealed record ScriptContext
{
    public Guid? RunbookId { get; init; }
    public string? RunbookCode { get; init; }
    public string? RunbookTitle { get; init; }
    public Guid? TaskId { get; init; }
    public string? TaskTitle { get; init; }
    public required string ExecutedBy { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

/// <summary>CSX calistirma sonucu.</summary>
public sealed record ScriptRunResult
{
    public ScriptExecutionStatus Status { get; init; }
    public string? Result { get; init; }
    public IReadOnlyList<string> Output { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }
    public long DurationMs { get; init; }
}

/// <summary>Script kaydi.</summary>
public sealed record ScriptDto
{
    public Guid Id { get; init; }
    public Guid? RunbookId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Code { get; init; }
    public int TimeoutSeconds { get; init; }
    public bool IsEnabled { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
}

/// <summary>Script olusturma/guncelleme istegi.</summary>
public sealed record SaveScriptRequest
{
    public Guid? RunbookId { get; init; }

    [Required, StringLength(150, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [Required, StringLength(100000, MinimumLength = 1)]
    public required string Code { get; init; }

    [Range(1, 900)]
    public int TimeoutSeconds { get; init; } = 60;

    public bool IsEnabled { get; init; } = true;
}

/// <summary>Script calistirma istegi.</summary>
public sealed record RunScriptRequest
{
    public Guid? TaskId { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
}

/// <summary>Ucuncu parti sisteme gonderilen olay.</summary>
public sealed record ExternalEvent
{
    public required string EventType { get; init; }
    public Guid RunbookId { get; init; }
    public required string RunbookCode { get; init; }
    public required string RunbookTitle { get; init; }
    public Guid? TaskId { get; init; }
    public string? TaskTitle { get; init; }
    public string? Status { get; init; }
    public required string ActorDisplayName { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Giden e-posta kuyrugu listeleme filtresi (yonetici ekrani, sadece test/izleme icin).</summary>
public sealed record EmailOutboxFilter
{
    public EmailStatus? Status { get; init; }
    public string? Reason { get; init; }
    public string? To { get; init; }
    public Guid? RunbookId { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

/// <summary>Giden e-posta kuyrugundaki tek kayit.</summary>
public sealed record EmailOutboxDto
{
    public long Id { get; init; }
    public required string To { get; init; }
    public string? Cc { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public EmailStatus Status { get; init; }
    public required string StatusText { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }
    public string? LastError { get; init; }
    public string? Reason { get; init; }
    public Guid? RunbookId { get; init; }
    public Guid? TaskId { get; init; }
}

/// <summary>Audit kaydi listeleme filtresi.</summary>
public sealed record AuditFilter
{
    public string? UserName { get; init; }
    public AuditAction? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public Guid? RunbookId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Bir takim adini uygulama rolune esleyen kayit (bkz. RoleMapping). Kullanicinin
/// rolu, uyesi oldugu AD gruplarindan degil, personel servisinin dondurdugu bu
/// takim adindan turetilir - eslesme yoksa Authorization:DefaultRole uygulanir.
/// </summary>
public sealed record RoleMappingDto
{
    public Guid Id { get; init; }
    public required string TeamName { get; init; }
    public AppRole Role { get; init; }
    public required string RoleText { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Yeni takim-rol eslemesi olusturma istegi.</summary>
public sealed record SaveRoleMappingRequest
{
    [Required, StringLength(256, MinimumLength = 1)]
    public required string TeamName { get; init; }

    public AppRole Role { get; init; }
}

/// <summary>Audit kaydi.</summary>
public sealed record AuditLogDto
{
    public long Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required string UserName { get; init; }
    public string? UserDisplayName { get; init; }
    public AuditAction Action { get; init; }
    public required string ActionText { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public Guid? RunbookId { get; init; }
    public string? Summary { get; init; }
    public string? Changes { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>Ana ekrandaki ozet kartlar.</summary>
public sealed record DashboardDto
{
    public int ActiveRunbooks { get; init; }
    public int DraftRunbooks { get; init; }
    public int TemplateCount { get; init; }
    public int MyOpenTasks { get; init; }
    public int MyTeamsOpenTasks { get; init; }
    public int BlockedTasks { get; init; }
    public IReadOnlyList<RunbookListItemDto> RecentRunbooks { get; init; } = Array.Empty<RunbookListItemDto>();
    public IReadOnlyList<MyTaskDto> MyTasks { get; init; } = Array.Empty<MyTaskDto>();
}

/// <summary>"Bana atanan gorevler" satiri.</summary>
public sealed record MyTaskDto
{
    public Guid TaskId { get; init; }
    public Guid RunbookId { get; init; }
    public required string RunbookCode { get; init; }
    public required string RunbookTitle { get; init; }
    public required string Title { get; init; }
    public required string ColorHex { get; init; }
    public RunbookTaskStatus Status { get; init; }
    public required string StatusText { get; init; }
    public TaskPriority Priority { get; init; }
    public DateTimeOffset? PlannedStart { get; init; }
    /// <summary>Atama kisiye mi yoksa uyesi oldugu bir gruba mi yapilmis.</summary>
    public required string AssignedVia { get; init; }
}
