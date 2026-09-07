using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Bir calisma/gecis planinin ust kaydi. Ayni varlik hem calisan runbook'u hem de
/// (<see cref="IsTemplate"/> = true iken) yeniden kullanilabilir sablonu temsil eder.
/// </summary>
public class Runbook : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Insan tarafindan okunabilir referans, orn. "RB-2026-0042".</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Yapilacak isin aciklamasi (Markdown destekli duz metin).</summary>
    public string? Description { get; set; }

    public RunbookStatus Status { get; set; } = RunbookStatus.Draft;

    /// <summary>true ise bu kayit calistirilmaz; yeni runbook uretmek icin sablondur.</summary>
    public bool IsTemplate { get; set; }

    /// <summary>Sablonlarin arayuzde gruplanmasi icin kategori, orn. "Veritabani Gecisi".</summary>
    public string? TemplateCategory { get; set; }

    /// <summary>
    /// Birden fazla runbook'u kapsayan ust baslik (Jira'daki "Epic" karsiligi),
    /// orn. "Karti Sistemler Online Gecisi" veya "Veri Merkezi Migrasyonu".
    /// Sablonlar disinda TUM runbook'larda kullanilabilir; serbest metindir.
    /// </summary>
    public string? SeyirName { get; set; }

    /// <summary>Bu runbook bir sablondan uretildiyse kaynak sablonun kimligi.</summary>
    public Guid? SourceTemplateId { get; set; }
    public Runbook? SourceTemplate { get; set; }

    public DateTimeOffset? PlannedStart { get; set; }
    public DateTimeOffset? PlannedEnd { get; set; }
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }

    /// <summary>Tamamlandi olarak isaretlenirken elle girilen gercek sure (dakika).
    /// ActualStart/ActualEnd farkindan bagimsizdir; runbook uzun sure acik kalmis
    /// olabilir, gercek harcanan efor burada ayrica belirtilir.</summary>
    public int? ActualMinutes { get; set; }

    /// <summary>Tamamlandi olarak isaretlenirken girilen, gelecege yonelik not
    /// (orn. "bir dahaki sefere X'e dikkat edilmeli").</summary>
    public string? CompletionNote { get; set; }

    /// <summary>
    /// true ise geri donus plani aktive edilmis: ana akistaki kapanmamis
    /// adimlar otomatik "Atlandi" yapilmis ve geri donus adimlarinin
    /// tarihleri hesaplanmistir (bkz. RunbookTask.IsRollbackStep,
    /// TaskService.StartRollbackAsync). Yalnizca bir kez, geri donduruleme
    /// bicimde aktive edilir.
    /// </summary>
    public bool IsRollbackActive { get; set; }

    /// <summary>
    /// Bos ise calisma ANA AKISI izliyor. Dolu ise (orn. "Senaryo-A") calisma o
    /// isimdeki alternatif senaryoya geçmis demektir: gecis aninda ana akistaki
    /// kapanmamis adimlar otomatik "Atlandi" yapilmis, o senaryonun adimlarinin
    /// tarihleri hesaplanmistir (bkz. RunbookTask.ScenarioGroup,
    /// TaskService.SwitchScenarioAsync). Yalnizca bir kez, geri donduruleme
    /// bicimde degistirilir.
    /// </summary>
    public string? ActiveScenarioGroup { get; set; }

    /// <summary>Runbook sahibi (AD kullanicisi).</summary>
    public Guid OwnerUserId { get; set; }
    public AppUser Owner { get; set; } = null!;

    /// <summary>Service Manager change/work item numarasi, orn. "CR12345".</summary>
    public string? ServiceManagerWorkItemId { get; set; }

    /// <summary>Serbest etiketler; virgulle ayrilmis olarak saklanir.</summary>
    public string? Tags { get; set; }

    /// <summary>Iyimser eszamanlilik damgasi (rowversion).</summary>
    public byte[]? RowVersion { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<RunbookTask> Tasks { get; set; } = new List<RunbookTask>();
    public ICollection<RunbookScript> Scripts { get; set; } = new List<RunbookScript>();
}
