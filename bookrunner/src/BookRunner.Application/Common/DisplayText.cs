using BookRunner.Domain.Enums;

namespace BookRunner.Application.Common;

/// <summary>Enum degerlerinin arayuzde gosterilecek Turkce karsiliklari.</summary>
public static class DisplayText
{
    public static string Status(RunbookStatus status) => status switch
    {
        RunbookStatus.Draft => "Taslak",
        RunbookStatus.Scheduled => "Planlandi",
        RunbookStatus.InProgress => "Devam Ediyor",
        RunbookStatus.Completed => "Tamamlandi",
        RunbookStatus.Cancelled => "Iptal Edildi",
        RunbookStatus.Archived => "Arsivlendi",
        _ => status.ToString()
    };

    public static string Status(RunbookTaskStatus status) => status switch
    {
        RunbookTaskStatus.NotStarted => "Baslamadi",
        RunbookTaskStatus.InProgress => "Devam Ediyor",
        RunbookTaskStatus.Blocked => "Bloke",
        RunbookTaskStatus.Completed => "Tamamlandi",
        RunbookTaskStatus.Failed => "Basarisiz",
        RunbookTaskStatus.Skipped => "Atlandi",
        _ => status.ToString()
    };

    public static string Priority(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "Dusuk",
        TaskPriority.Normal => "Normal",
        TaskPriority.High => "Yuksek",
        TaskPriority.Critical => "Kritik",
        _ => priority.ToString()
    };

    public static string Activity(TaskActivityType type) => type switch
    {
        TaskActivityType.Created => "Olusturuldu",
        TaskActivityType.Updated => "Guncellendi",
        TaskActivityType.StatusChanged => "Durum degisti",
        TaskActivityType.Assigned => "Atandi",
        TaskActivityType.Unassigned => "Atama kaldirildi",
        TaskActivityType.HandedOver => "Devredildi",
        TaskActivityType.Commented => "Yorum yapildi",
        TaskActivityType.Started => "Baslatildi",
        TaskActivityType.Completed => "Tamamlandi",
        TaskActivityType.Blocked => "Bloke edildi",
        TaskActivityType.ScriptExecuted => "Script calistirildi",
        TaskActivityType.Reordered => "Sirasi degisti",
        _ => type.ToString()
    };

    public static string Action(AuditAction action) => action switch
    {
        AuditAction.Create => "Olusturma",
        AuditAction.Update => "Guncelleme",
        AuditAction.Delete => "Silme",
        AuditAction.Read => "Okuma",
        AuditAction.Export => "Disa aktarma",
        AuditAction.Import => "Ice aktarma",
        AuditAction.Execute => "Calistirma",
        AuditAction.Login => "Oturum acma",
        AuditAction.PermissionDenied => "Yetkisiz erisim",
        _ => action.ToString()
    };

    public static string Role(AppRole role) => role switch
    {
        AppRole.Viewer => "Izleyici",
        AppRole.Contributor => "Katilimci",
        AppRole.RunbookAuthor => "Runbook Yazari",
        AppRole.Administrator => "Yonetici",
        _ => role.ToString()
    };

    public static string EmailStatus(Domain.Enums.EmailStatus status) => status switch
    {
        Domain.Enums.EmailStatus.Pending => "Beklemede",
        Domain.Enums.EmailStatus.Sent => "Gonderildi",
        Domain.Enums.EmailStatus.Failed => "Basarisiz",
        Domain.Enums.EmailStatus.Cancelled => "Iptal",
        _ => status.ToString()
    };
}
