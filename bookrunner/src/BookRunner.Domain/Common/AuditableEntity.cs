namespace BookRunner.Domain.Common;

/// <summary>
/// Kim, ne zaman olusturdu/guncelledi bilgisini tasiyan taban sinif.
/// Alanlar <c>AuditSaveChangesInterceptor</c> tarafindan otomatik doldurulur.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Olusturan kullanicinin sAMAccountName degeri (DOMAIN\kullanici).</summary>
    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary>Soft-delete destegi olan varliklar icin isaretleyici.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
