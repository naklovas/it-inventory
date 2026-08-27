namespace BookRunner.Domain.Entities;

/// <summary>
/// Active Directory guvenlik/dagitim grubunun yerel kopyasi.
/// Gorev atamalari kisilere oldugu kadar dogrudan bu gruplara da yapilabilir.
/// </summary>
public class AppGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>AD objectSid.</summary>
    public string Sid { get; set; } = string.Empty;

    /// <summary>sAMAccountName (orn. "SRV-DBA-Team").</summary>
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Email { get; set; }

    public string? DistinguishedName { get; set; }

    /// <summary>Grup rozetinin arayuzdeki rengi (#RRGGBB).</summary>
    public string AvatarColor { get; set; } = "#7A5AA8";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastSyncedAt { get; set; }

    public ICollection<AppUserGroup> Members { get; set; } = new List<AppUserGroup>();
}

/// <summary>Kullanici - grup uyeligi (AD'den senkronize edilir).</summary>
public class AppUserGroup
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid GroupId { get; set; }
    public AppGroup Group { get; set; } = null!;

    public DateTimeOffset SyncedAt { get; set; }
}
