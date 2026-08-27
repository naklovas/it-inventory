using System.ComponentModel.DataAnnotations;

namespace BookRunner.Infrastructure.Directory;

/// <summary>Active Directory baglanti ayarlari (appsettings: "ActiveDirectory").</summary>
public sealed class ActiveDirectoryOptions
{
    public const string SectionName = "ActiveDirectory";

    /// <summary>Etki alani adi. Bos birakilirsa sunucunun uyesi oldugu etki alani kullanilir.</summary>
    public string? Domain { get; set; }

    /// <summary>Aramanin baslayacagi OU, orn. "OU=Users,DC=contoso,DC=com". Bos ise etki alani koku.</summary>
    public string? SearchRoot { get; set; }

    /// <summary>
    /// Okuma icin kullanilacak servis hesabi. Bos birakilirsa uygulama havuzunun
    /// kimligiyle baglanilir (onerilen yaklasim).
    /// </summary>
    public string? ServiceAccountUserName { get; set; }

    public string? ServiceAccountPassword { get; set; }

    /// <summary>AD sorgu sonuclarinin onbellekte tutulma suresi (dakika).</summary>
    [Range(1, 1440)]
    public int CacheMinutes { get; set; } = 30;

    /// <summary>Tek bir aramada AD'den donecek en fazla kayit sayisi.</summary>
    [Range(1, 1000)]
    public int MaxSearchResults { get; set; } = 50;

    /// <summary>Kullanici fotografinin okundugu AD nitelikleri (sirayla denenir).</summary>
    public string[] PhotoAttributes { get; set; } = ["thumbnailPhoto", "jpegPhoto"];

    /// <summary>
    /// AD'ye hic erisilemeyen ortamlarda (gelistirici makinesi) true yapilir;
    /// bu durumda dizin sorgulari bos sonuc doner ve uygulama cokmez.
    /// </summary>
    public bool Disabled { get; set; }
}
