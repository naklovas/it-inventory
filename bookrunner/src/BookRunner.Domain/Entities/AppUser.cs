using BookRunner.Domain.Common;

namespace BookRunner.Domain.Entities;

/// <summary>
/// Active Directory'den okunan kullanicinin yerel kopyasi (onbellek/projeksiyon).
/// Kullanici bilgileri AD'de yonetilir; burada yalnizca uygulamanin ihtiyac duydugu
/// alanlar (ad, e-posta, unvan, foto) periyodik olarak senkronize edilir.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>AD objectSid. Kullanicinin degismeyen benzersiz kimligi.</summary>
    public string Sid { get; set; } = string.Empty;

    /// <summary>DOMAIN\kullanici formatindaki oturum adi.</summary>
    public string SamAccountName { get; set; } = string.Empty;

    public string? UserPrincipalName { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Title { get; set; }

    public string? Department { get; set; }

    /// <summary>
    /// Personel servisinden (bkz. IPersonnelDirectoryService) gelen takim adi.
    /// Rol eslemesi (RoleMapping) icin taze cekilir; burada kalici tutulmasinin
    /// tek amaci takim bazli siralama tablosudur (oyunlastirma).
    /// </summary>
    public string? TeamName { get; set; }

    public string? Company { get; set; }

    public string? OfficePhone { get; set; }

    public string? MobilePhone { get; set; }

    /// <summary>AD'deki yoneticinin distinguishedName degeri.</summary>
    public string? ManagerDistinguishedName { get; set; }

    public string? DistinguishedName { get; set; }

    /// <summary>AD thumbnailPhoto / jpegPhoto icerigi. Yoksa arayuz bas harflerini gosterir.</summary>
    public byte[]? Photo { get; set; }

    public string? PhotoContentType { get; set; }

    /// <summary>Foto degistiginde ETag/cache kirilimi icin kullanilir.</summary>
    public string? PhotoHash { get; set; }

    /// <summary>Ad-soyad bas harfleri. Foto yoksa avatar olarak gosterilir.</summary>
    public string Initials { get; set; } = string.Empty;

    /// <summary>Avatar arka plan rengi (#RRGGBB). Sid'den deterministik uretilir.</summary>
    public string AvatarColor { get; set; } = "#5B6ABF";

    /// <summary>AD hesabi devre disi/silinmis ise false.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastSyncedAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public ICollection<AppUserGroup> Groups { get; set; } = new List<AppUserGroup>();
}
