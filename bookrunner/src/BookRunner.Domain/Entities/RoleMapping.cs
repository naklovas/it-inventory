using BookRunner.Domain.Common;
using BookRunner.Domain.Enums;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Bir takim adini uygulama rolune esler. Takim adi personel servisinden
/// (bkz. IPersonnelDirectoryService) gelir; yetki AD grup uyeliginden degil
/// buradan turetilir.
/// </summary>
public class RoleMapping : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Personel servisinin dondurdugu "teamName" degeriyle birebir eslenir.</summary>
    public string TeamName { get; set; } = string.Empty;

    public AppRole Role { get; set; }

    public bool IsActive { get; set; } = true;
}
