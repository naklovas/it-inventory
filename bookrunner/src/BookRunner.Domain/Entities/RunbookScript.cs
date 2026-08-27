using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Runbook adimlarina baglanabilen Roslyn C# script'i (CSX). Script'ler yalnizca
/// Administrator rolundeki kullanicilar tarafindan olusturulup calistirilabilir.
/// </summary>
public class RunbookScript : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Runbook'a ozel script ise dolu; kutuphane script'i ise null.</summary>
    public Guid? RunbookId { get; set; }
    public Runbook? Runbook { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>CSX kaynak kodu.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Calistirma zaman asimi (saniye).</summary>
    public int TimeoutSeconds { get; set; } = 60;

    public bool IsEnabled { get; set; } = true;

    public ICollection<ScriptExecution> Executions { get; set; } = new List<ScriptExecution>();
}

/// <summary>Bir script calistirmasinin sonucu; audit trail'in parcasidir.</summary>
public class ScriptExecution
{
    public long Id { get; set; }

    public Guid ScriptId { get; set; }
    public RunbookScript Script { get; set; } = null!;

    /// <summary>Script bir gorev baglaminda calistirildiysa dolu.</summary>
    public Guid? TaskId { get; set; }
    public RunbookTask? Task { get; set; }

    public Guid? ExecutedByUserId { get; set; }
    public AppUser? ExecutedBy { get; set; }

    public ScriptExecutionStatus Status { get; set; } = ScriptExecutionStatus.Running;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Script'in dondurdugu degerin metin gosterimi.</summary>
    public string? Result { get; set; }

    /// <summary>Script icinden Log(...) ile yazilan satirlar.</summary>
    public string? Output { get; set; }

    public string? Error { get; set; }
}
