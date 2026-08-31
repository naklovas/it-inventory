using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Oyunlastirma puan olayinin denetim izi. Toplam puan bu tablonun SUM'u olarak
/// hesaplanir; boylece "neden bu puani aldim" sorusu her zaman yanitlanabilir ve
/// puan degerleri (appsettings) sonradan degisse bile gecmis kayitlar bozulmaz.
/// </summary>
public class GamificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public GamificationEventType EventType { get; set; }

    /// <summary>Negatif olabilir (orn. basarisiz/geri alinan gorev cezasi).</summary>
    public int Points { get; set; }

    public Guid? RunbookId { get; set; }

    public Guid? RunbookTaskId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
