using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;

namespace BookRunner.Web.Models;

/// <summary>Tum sayfalarin ortak ihtiyaci: oturum acan kullanici ve yetkileri.</summary>
public class PageViewModel
{
    public CurrentUserDto? CurrentUser { get; init; }

    public bool Can(string permission)
        => CurrentUser?.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) == true;

    /// <summary>Yeni runbook/sablon acabilir (bos veya sablondan). Herkese acik varsayilan roldur.</summary>
    public bool CanCreateRunbook => Can(Permissions.RunbookCreate);
    /// <summary>BASKASININ runbook'unu da duzenleyebilir (yonetici/yazar). Kendi runbook'unu duzenlemek icin gerekmez.</summary>
    public bool CanWrite => Can(Permissions.RunbookWrite);
    public bool CanDeleteTask => Can(Permissions.TaskDelete);
    public bool CanAssign => Can(Permissions.TaskAssign);
    public bool CanComment => Can(Permissions.TaskComment);
    public bool CanExecute => Can(Permissions.TaskExecute);
    public bool CanExport => Can(Permissions.ExportData);
    public bool CanImport => Can(Permissions.ImportData);
    public bool CanPublishTemplate => Can(Permissions.RunbookPublishTemplate);
    public bool CanDelete => Can(Permissions.RunbookDelete);
    public bool CanViewAudit => Can(Permissions.AuditRead);
    public bool CanRunScript => Can(Permissions.ScriptExecute);
    public bool CanManageAdmin => Can(Permissions.AdminManage);
}

/// <summary>Ana ekran.</summary>
public sealed class DashboardViewModel : PageViewModel
{
    public DashboardDto Dashboard { get; init; } = new();
}

/// <summary>Liderlik tablosu: bireysel ve takim siralamasi, rozetlerim.</summary>
public sealed class LeaderboardViewModel : PageViewModel
{
    public LeaderboardPeriod Period { get; init; }
    public IReadOnlyList<LeaderboardEntryDto> Users { get; init; } = [];
    public IReadOnlyList<TeamLeaderboardEntryDto> Teams { get; init; } = [];
    public IReadOnlyList<BadgeDto> MyBadges { get; init; } = [];
}

/// <summary>Runbook listesi ve filtreleri.</summary>
public sealed class RunbookListViewModel : PageViewModel
{
    public RunbookFilter Filter { get; init; } = new();
    public PagedResult<RunbookListItemDto> Results { get; init; } = PagedResult<RunbookListItemDto>.Create([], 1, 25, 0);
    public bool TemplatesView { get; init; }
    /// <summary>"Seyir" (ust baslik) suzme listesi icin mevcut tekil degerler.</summary>
    public IReadOnlyList<string> SeyirNames { get; init; } = Array.Empty<string>();
}

/// <summary>Runbook detay ekrani (gorev barlari, yorumlar, tarihce).</summary>
public sealed class RunbookDetailViewModel : PageViewModel
{
    public required RunbookDetailDto Runbook { get; init; }

    /// <summary>
    /// Oturum acan kullanici bu runbook'un sahibi mi. Sahip, rol izni olmasa da
    /// kendi runbook'unda her degisikligi yapabilir (API tarafinda da ayni kural
    /// IRunbookAccess ile uygulanir; buradaki bayraklar yalnizca arayuzu sekillendirir).
    /// </summary>
    public bool IsOwner => CurrentUser?.Id is { } userId && Runbook.Owner?.Id == userId;

    /// <summary>
    /// Sahibin bu runbook'a ozel olarak "Editor" olarak ekledigi kisi mi.
    /// Global role dokunmadan yalnizca gorev yazma/atama/yorum acar - runbook'u
    /// duzenleyemez/silemez, sablon yayinlayamaz, disa/ice aktaramaz.
    /// </summary>
    public bool IsCollaborator => CurrentUser?.Id is { } userId && Runbook.Collaborators.Any(c => c.Person.Id == userId);

    /// <summary>Editor ekleme/kaldirma yalnizca sahipte - yonetici rolu bile atlayamaz.</summary>
    public bool CanManageCollaborators => IsOwner;

    public bool CanEditThis => IsOwner || IsCollaborator || CanWrite;
    public bool CanAssignThis => IsOwner || IsCollaborator || CanAssign;
    public bool CanExecuteThis => IsOwner || CanExecute;
    public bool CanCommentThis => IsOwner || IsCollaborator || CanComment;
    public bool CanImportThis => IsOwner || CanImport;
    public bool CanPublishTemplateThis => IsOwner || CanPublishTemplate;

    /// <summary>Runbook silme: yonetici rolu veya runbook sahibi.</summary>
    public bool CanDeleteRunbookThis => IsOwner || CanDelete;

    /// <summary>Gorev silme: yonetici rolu veya runbook sahibi.</summary>
    public bool CanDeleteTaskThis => IsOwner || CanDeleteTask;

    /// <summary>SignalR hub adresi; canli guncellemeler icin.</summary>
    public string HubUrl { get; init; } = string.Empty;

    public IReadOnlyList<ScriptDto> Scripts { get; init; } = Array.Empty<ScriptDto>();

    public int TotalTasks => Runbook.Tasks.Count;

    public int CompletedTasks => Runbook.Tasks.Count(t =>
        t.Status is Domain.Enums.RunbookTaskStatus.Completed or Domain.Enums.RunbookTaskStatus.Skipped);

    public int ProgressPercent => TotalTasks == 0 ? 0 : (int)Math.Round(CompletedTasks * 100.0 / TotalTasks);
}

/// <summary>Runbook olusturma/duzenleme formu.</summary>
public sealed class RunbookFormViewModel : PageViewModel
{
    public Guid? Id { get; set; }
    public string? Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Domain.Enums.RunbookStatus Status { get; set; } = Domain.Enums.RunbookStatus.Draft;
    public bool IsTemplate { get; set; }
    public string? TemplateCategory { get; set; }
    /// <summary>Birden fazla runbook'u kapsayan ust baslik (bkz. Runbook.SeyirName).</summary>
    public string? SeyirName { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public string? ServiceManagerWorkItemId { get; set; }
    public string? TagsText { get; set; }
    public string? RowVersion { get; set; }

    /// <summary>Seyir alaninda otomatik tamamlama icin mevcut seyir adlari.</summary>
    public IReadOnlyList<string> SeyirNames { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Tags => string.IsNullOrWhiteSpace(TagsText)
        ? Array.Empty<string>()
        : TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Audit trail ekrani.</summary>
public sealed class AuditViewModel : PageViewModel
{
    public AuditFilter Filter { get; init; } = new();
    public PagedResult<AuditLogDto> Results { get; init; } = PagedResult<AuditLogDto>.Create([], 1, 50, 0);
}

/// <summary>
/// Giden e-posta kuyrugu ekrani. Email:Enabled=false iken de her bildirim
/// buraya yazilir; boylece gercek mail atmadan hangi olayin kime, ne konuda
/// bildirim urettigi test edilebilir.
/// </summary>
public sealed class EmailOutboxViewModel : PageViewModel
{
    public EmailOutboxFilter Filter { get; init; } = new();
    public PagedResult<EmailOutboxDto> Results { get; init; } = PagedResult<EmailOutboxDto>.Create([], 1, 50, 0);
}

/// <summary>Yonetim / entegrasyon durumu ekrani.</summary>
public sealed class AdminViewModel : PageViewModel
{
    public ServiceManagerHealth? ServiceManager { get; init; }
    public IReadOnlyList<ScriptDto> Scripts { get; init; } = Array.Empty<ScriptDto>();
}

/// <summary>
/// Takim adi -> rol eslemeleri ekrani. Bir takima rol atamak, personel
/// servisinden o takim adi gelen HERKESE o rolun yetkilerini vermek demektir.
/// </summary>
public sealed class RoleMappingsViewModel : PageViewModel
{
    public IReadOnlyList<RoleMappingDto> Mappings { get; init; } = Array.Empty<RoleMappingDto>();
}

/// <summary>Hata sayfasi.</summary>
public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }
    public string? Message { get; init; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
