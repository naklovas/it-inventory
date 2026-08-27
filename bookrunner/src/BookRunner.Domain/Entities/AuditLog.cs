using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Degismez audit kaydi. Varlik degisiklikleri EF Core interceptor'i tarafindan,
/// disa aktarim/script gibi islemler ise servisler tarafindan yazilir.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Islemi yapan Windows hesabi (DOMAIN\kullanici).</summary>
    public string UserName { get; set; } = string.Empty;

    public string? UserDisplayName { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Etkilenen varlik turu, orn. "Runbook".</summary>
    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    /// <summary>Iliskili runbook; audit ekraninda filtreleme icin.</summary>
    public Guid? RunbookId { get; set; }

    /// <summary>Degisen alanlar {alan: {eski, yeni}} seklinde JSON.</summary>
    public string? Changes { get; set; }

    public string? Summary { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Istegi uctan uca izlemek icin korelasyon kimligi.</summary>
    public string? CorrelationId { get; set; }
}
