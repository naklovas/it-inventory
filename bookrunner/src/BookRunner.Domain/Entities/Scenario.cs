using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Bir runbook'ta tanimli alternatif senaryonun kendisi (adimlarindan BAGIMSIZ
/// olarak var olur). Once senaryo burada olusturulur (ad, opsiyonel tetikleyici
/// gorev/kosul, opsiyonel rejoin noktasi); adimlar daha sonra RunbookTask.
/// ScenarioGroup = bu senaryonun Name'i ile eslenerek eklenir. Boylece bir
/// senaryo hic adimi olmadan da (henuz adim eklenmeden) listelenebilir.
/// </summary>
public class Scenario : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunbookId { get; set; }
    public Runbook Runbook { get; set; } = null!;

    /// <summary>Senaryo adi, orn. "Senaryo-A". RunbookTask.ScenarioGroup ile
    /// (harf buyuk/kucuk duyarsiz olmadan, birebir) eslesir.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Dolu ise, bu senaryonun TUM adimlari kapandiginda ana runbook bu
    /// gorevden (rejoin noktasi) devam eder. Bos ise senaryo tek yonludur.
    /// Yalnizca ANA AKIS gorevi olabilir.
    /// </summary>
    public Guid? RejoinTaskId { get; set; }
    public RunbookTask? RejoinTask { get; set; }

    /// <summary>
    /// Senaryonun TUMU basarisiz sayildiginda (bu senaryo aktifken herhangi bir
    /// adimi Basarisiz olarak isaretlendiginde) otomatik tetiklenecek eylem.
    /// Senaryo adimlarinin kendi basina bir FailureAction'i yoktur (bkz.
    /// RunbookTask.FailureAction, TaskService.CreateAsync) - bu, onun senaryo
    /// seviyesindeki karsiligidir. Su an icin yalnizca None/StartRollback
    /// desteklenir (bkz. TaskService.CreateScenarioAsync).
    /// </summary>
    public TaskFailureAction FailureAction { get; set; } = TaskFailureAction.None;

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
