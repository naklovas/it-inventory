namespace BookRunner.Domain.Entities;

/// <summary>Kazanilabilir basarinin sabit tanimi (kod ile senkron statik katalog).</summary>
public class Badge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kod tarafindaki esik kontrolunun anahtari, orn. "TASKS_10".</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    /// <summary>Bootstrap Icons sinif adi, orn. "bi-award".</summary>
    public required string Icon { get; set; }

    public int SortOrder { get; set; }
}
