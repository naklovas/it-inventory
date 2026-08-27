using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Bir AD grubunu uygulama rolune esler. Uygulamada ayri bir kullanici/rol yonetimi
/// yoktur; yetki tamamen AD grup uyeliginden turetilir.
/// </summary>
public class RoleMapping : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>AD grubunun objectSid degeri.</summary>
    public string GroupSid { get; set; } = string.Empty;

    /// <summary>Kolay okunabilirlik icin grup adi (yetki karari SID uzerinden verilir).</summary>
    public string GroupName { get; set; } = string.Empty;

    public AppRole Role { get; set; }

    public bool IsActive { get; set; } = true;
}
