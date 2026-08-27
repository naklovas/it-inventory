using BookRunner.Application.Dtos;

namespace BookRunner.Application.Abstractions;

/// <summary>E-posta gonderimi. Uygulama kuyruga yazar, gonderimi arka plan servisi yapar.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Bildirim metinlerini uretir ve giden kuyruguna yazar.</summary>
public interface INotificationService
{
    /// <summary>Goreve atanan kisilere / grup uyelerine bildirim gonderir.</summary>
    Task NotifyTaskAssignedAsync(Guid taskId, Guid assignmentId, CancellationToken ct = default);

    /// <summary>Gorev devredildiginde hem yeni sahibe hem devredene bildirim gonderir.</summary>
    Task NotifyTaskHandedOverAsync(Guid taskId, Guid newAssignmentId, string? note, CancellationToken ct = default);

    /// <summary>Yorumda anilan kisilere ve gorev sahiplerine bildirim gonderir.</summary>
    Task NotifyTaskCommentedAsync(Guid taskId, Guid commentId, CancellationToken ct = default);

    Task NotifyTaskStatusChangedAsync(Guid taskId, string oldStatus, string newStatus, CancellationToken ct = default);
}

/// <summary>Runbook'un Excel'e aktarimi ve Excel'den gorev ice aktarimi.</summary>
public interface IExcelService
{
    Task<byte[]> ExportRunbookAsync(Guid runbookId, CancellationToken ct = default);

    /// <summary>Filtrelenmis runbook listesini tek sayfa halinde disa aktarir.</summary>
    Task<byte[]> ExportRunbookListAsync(RunbookFilter filter, CancellationToken ct = default);

    /// <summary>Gorevleri ice aktarmak icin bos sablon uretir.</summary>
    byte[] CreateImportTemplate();

    /// <summary>Excel dosyasindaki gorevleri okur; dogrulama hatalarini sonucla birlikte dondurur.</summary>
    Task<ImportResult> ImportTasksAsync(Guid runbookId, Stream excelStream, bool commit, CancellationToken ct = default);
}

/// <summary>Runbook'un PDF ciktisi.</summary>
public interface IPdfService
{
    Task<byte[]> ExportRunbookAsync(Guid runbookId, CancellationToken ct = default);
}

/// <summary>Audit trail yazma islemleri.</summary>
public interface IAuditService
{
    Task LogAsync(
        Domain.Enums.AuditAction action,
        string entityType,
        string? entityId,
        string summary,
        Guid? runbookId = null,
        object? changes = null,
        CancellationToken ct = default);
}

/// <summary>
/// Service Manager veritabanina (DW/CMDB) salt-okunur erisim.
/// SCSM konsolu/SDK'si yerine dogrudan SQL uzerinden calisir.
/// </summary>
public interface IServiceManagerReader
{
    Task<IReadOnlyList<ServiceManagerWorkItem>> SearchWorkItemsAsync(string term, int take, CancellationToken ct = default);

    Task<ServiceManagerWorkItem?> GetWorkItemAsync(string id, CancellationToken ct = default);

    /// <summary>Baglantiyi ve yapilandirilan sorguyu dogrular (yonetim ekrani icin).</summary>
    Task<ServiceManagerHealth> CheckHealthAsync(CancellationToken ct = default);
}

/// <summary>Roslyn (CSX) script calistirici.</summary>
public interface IScriptRunner
{
    Task<ScriptRunResult> RunAsync(string code, ScriptContext context, int timeoutSeconds, CancellationToken ct = default);
}

/// <summary>Ucuncu parti REST API entegrasyonu (ITSM/webhook/sohbet kanali).</summary>
public interface IExternalIntegrationClient
{
    /// <summary>Entegrasyonun etkin olup olmadigi.</summary>
    bool IsEnabled { get; }

    /// <summary>Runbook/gorev olayini disaridaki sisteme iletir.</summary>
    Task<bool> PublishEventAsync(ExternalEvent payload, CancellationToken ct = default);
}

/// <summary>
/// Canli isbirligi bildirimleri (SignalR). Soyutlama sayesinde is katmani
/// SignalR'a dogrudan bagimli olmaz.
/// </summary>
public interface IRealtimeNotifier
{
    Task TaskChangedAsync(Guid runbookId, Guid taskId, string changeType, CancellationToken ct = default);

    Task CommentAddedAsync(Guid runbookId, Guid taskId, object comment, CancellationToken ct = default);

    Task RunbookChangedAsync(Guid runbookId, string changeType, CancellationToken ct = default);
}
