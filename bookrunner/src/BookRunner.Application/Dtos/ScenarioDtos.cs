using System.ComponentModel.DataAnnotations;
using BookRunner.Domain.Enums;

namespace BookRunner.Application.Dtos;

/// <summary>Bir senaryonun kendisi (adimlarindan bagimsiz - bkz. Domain.Entities.Scenario).</summary>
public sealed record ScenarioDto
{
    public Guid Id { get; init; }
    public Guid RunbookId { get; init; }
    public required string Name { get; init; }

    public Guid? RejoinTaskId { get; init; }
    public string? RejoinTaskTitle { get; init; }

    /// <summary>Bu senaryoyu otomatik tetikleyen gorev (varsa) - RunbookTask.FailureScenarioGroup/
    /// SuccessScenarioGroup uzerinden bulunur, Scenario uzerinde ayrica saklanmaz.</summary>
    public Guid? TriggerTaskId { get; init; }
    public string? TriggerTaskTitle { get; init; }
    public ScenarioTriggerCondition? TriggerCondition { get; init; }

    public int TaskCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Yeni senaryo olusturma istegi (henuz adimsiz - bkz. TaskService.CreateScenarioAsync).</summary>
public sealed record CreateScenarioRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>Doldurulursa, bu senaryo TUM adimlariyla kapandiginda ana runbook'un
    /// devam edecegi ANA AKIS gorevi. Bos ise senaryo tek yonludur.</summary>
    public Guid? RejoinTaskId { get; init; }

    /// <summary>Doldurulursa, bu gorev belirtilen kosulda olunca senaryo otomatik
    /// baslatilir (gorevin FailureAction/SuccessScenarioGroup alanlarina yazilir).
    /// Ana akis veya baska bir senaryonun adimi olabilir.</summary>
    public Guid? TriggerTaskId { get; init; }

    /// <summary>TriggerTaskId doluyken zorunlu: Basarisiz mi Basarili mi olunca tetiklenecek.</summary>
    public ScenarioTriggerCondition? TriggerCondition { get; init; }
}
