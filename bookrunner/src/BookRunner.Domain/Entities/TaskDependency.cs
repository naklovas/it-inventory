namespace BookRunner.Domain.Entities;

/// <summary>
/// Bir gorevin (<see cref="TaskId"/>, "ardil") baska bir goreve (<see cref="DependsOnTaskId"/>,
/// "oncul") bagimliligi. Bir gorevin birden fazla onculu ve birden fazla ardili
/// olabilir; bu yuzden tekli bir FK yerine ayri bir iliski tablosu kullanilir.
/// Ardil gorev, TUM oncullari kapanmadan (Tamamlandi/Atlandi) baslatilamaz/
/// tamamlanamaz (bkz. TaskService.ChangeStatusAsync).
/// </summary>
public class TaskDependency
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ardil (bagimli) gorev.</summary>
    public Guid TaskId { get; set; }
    public RunbookTask Task { get; set; } = null!;

    /// <summary>Oncul (once kapanmasi gereken) gorev.</summary>
    public Guid DependsOnTaskId { get; set; }
    public RunbookTask DependsOnTask { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
