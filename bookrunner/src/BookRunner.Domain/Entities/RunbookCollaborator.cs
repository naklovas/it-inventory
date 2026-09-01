namespace BookRunner.Domain.Entities;

/// <summary>
/// Runbook sahibinin, KENDI runbook'una ozel olarak (global role dokunmadan)
/// bir kisiye "Editor" yetkisi (gorev ekleme/duzenleme, atama, yorum) verdigi
/// kayit. Sadece runbook sahibi ekleyip kaldirabilir.
/// </summary>
public class RunbookCollaborator
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunbookId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public string AddedBy { get; set; } = string.Empty;
}
