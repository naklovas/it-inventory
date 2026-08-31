namespace BookRunner.Domain.Enums;

/// <summary>Runbook'un yasam dongusundeki durumu.</summary>
public enum RunbookStatus
{
    Draft = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Archived = 5
}

/// <summary>Runbook icindeki tek bir gorevin durumu.</summary>
public enum RunbookTaskStatus
{
    NotStarted = 0,
    InProgress = 1,
    Blocked = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5
}

/// <summary>Gorevin oncelik seviyesi.</summary>
public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>Bir gorevin kisiye mi yoksa AD grubuna mi atandigi.</summary>
public enum AssigneeType
{
    User = 0,
    Group = 1
}

/// <summary>Gorev tarihcesinde (akordiyon) gosterilen olay turleri.</summary>
public enum TaskActivityType
{
    Created = 0,
    Updated = 1,
    StatusChanged = 2,
    Assigned = 3,
    Unassigned = 4,
    HandedOver = 5,
    Commented = 6,
    Started = 7,
    Completed = 8,
    Blocked = 9,
    ScriptExecuted = 10,
    Reordered = 11
}

/// <summary>Audit trail kaydinin islem turu.</summary>
public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Read = 3,
    Export = 4,
    Import = 5,
    Execute = 6,
    Login = 7,
    PermissionDenied = 8
}

/// <summary>Uygulama ici yetki seviyeleri. AD gruplari bu rollere eslenir.</summary>
public enum AppRole
{
    /// <summary>Sadece okuma.</summary>
    Viewer = 0,
    /// <summary>Kendisine atanan gorevleri yurutur, yorum yazar, devreder.</summary>
    Contributor = 1,
    /// <summary>Runbook olusturur, gorev atar, sablon yayinlar.</summary>
    RunbookAuthor = 2,
    /// <summary>Tum runbook'lar uzerinde tam yetki.</summary>
    Administrator = 3
}

/// <summary>Kuyruga alinan e-postanin durumu.</summary>
public enum EmailStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Cancelled = 3
}

/// <summary>CSX script calistirma sonucu.</summary>
public enum ScriptExecutionStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    TimedOut = 3
}

/// <summary>Oyunlastirma puan olayinin turu; denetim izinde ("neden bu puani aldim") kullanilir.</summary>
public enum GamificationEventType
{
    TaskCompleted = 0,
    TaskOnTimeBonus = 1,
    TaskFailedPenalty = 2,
    RunbookCompleted = 3,
    CommentAdded = 4
}
