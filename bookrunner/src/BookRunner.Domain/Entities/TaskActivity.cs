using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Goreve ait degismez olay kaydi. Arayuzde goreve tiklaninca acilan akordiyon
/// tarihcesini besler (durum degisiklikleri, atamalar, devirler, yorumlar...).
/// </summary>
public class TaskActivity
{
    public long Id { get; set; }

    public Guid TaskId { get; set; }
    public RunbookTask Task { get; set; } = null!;

    public TaskActivityType Type { get; set; }

    /// <summary>Islemi yapan kullanici. Sistem tarafindan uretildiyse null.</summary>
    public Guid? ActorUserId { get; set; }
    public AppUser? Actor { get; set; }

    /// <summary>Aktoru uygulama disinda tutabilmek icin duz metin ad (orn. "SYSTEM").</summary>
    public string ActorDisplayName { get; set; } = string.Empty;

    /// <summary>Onceki deger (durum adi, atanan kisi vb.).</summary>
    public string? OldValue { get; set; }

    /// <summary>Yeni deger.</summary>
    public string? NewValue { get; set; }

    /// <summary>Arayuzde gosterilecek hazir ozet metin.</summary>
    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
