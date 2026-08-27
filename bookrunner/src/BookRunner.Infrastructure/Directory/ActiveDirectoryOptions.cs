using System.ComponentModel.DataAnnotations;

namespace BookRunner.Infrastructure.Directory;

/// <summary>Active Directory baglanti ayarlari (appsettings: "ActiveDirectory").</summary>
public sealed class ActiveDirectoryOptions
{
    public const string SectionName = "ActiveDirectory";

    /// <summary>
    /// Birincil etki alani adi. Bos birakilirsa sunucunun uyesi oldugu etki alani
    /// kullanilir. Birden fazla etki alani varsa <see cref="Domains"/> listesini doldurun.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>Aramanin baslayacagi OU, orn. "OU=Users,DC=contoso,DC=com". Bos ise etki alani koku.</summary>
    public string? SearchRoot { get; set; }

    /// <summary>
    /// Okuma icin kullanilacak servis hesabi. Bos birakilirsa uygulama havuzunun
    /// kimligiyle baglanilir (onerilen yaklasim).
    /// </summary>
    public string? ServiceAccountUserName { get; set; }

    public string? ServiceAccountPassword { get; set; }

    /// <summary>
    /// Birden fazla etki alani (orman icindeki alt domainler veya guven iliskisi olan
    /// domainler) icin liste. Bos birakilirsa <see cref="Domain"/> / <see cref="SearchRoot"/>
    /// degerlerinden tek bir etki alani turetilir.
    /// Arama sorgulari tum etki alanlarinda calisir; SID cozumlemeleri ilk eslesmede durur.
    /// </summary>
    public List<DirectoryDomainOptions> Domains { get; set; } = [];

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

    /// <summary>
    /// Sorgulanacak etki alanlarini dondurur. <see cref="Domains"/> bos ise
    /// kok ayarlardan tek elemanli bir liste uretir; boylece tek domainli
    /// kurulumlarda ek yapilandirma gerekmez.
    /// </summary>
    public IReadOnlyList<DirectoryDomainOptions> ResolveDomains()
    {
        if (Domains.Count > 0)
        {
            return Domains;
        }

        return
        [
            new DirectoryDomainOptions
            {
                Name = Domain,
                SearchRoot = SearchRoot,
                ServiceAccountUserName = ServiceAccountUserName,
                ServiceAccountPassword = ServiceAccountPassword
            }
        ];
    }
}

/// <summary>Tek bir Active Directory etki alaninin ayarlari.</summary>
public sealed class DirectoryDomainOptions
{
    /// <summary>DNS etki alani adi, orn. "contoso.com".</summary>
    public string? Name { get; set; }

    /// <summary>
    /// NetBIOS adi, orn. "CONTOSO". Kullanici "CONTOSO\ali" seklinde oturum
    /// actiginda dogru etki alanina once sorulmasini saglar.
    /// </summary>
    public string? NetBiosName { get; set; }

    /// <summary>Aramanin baslayacagi OU/kok, orn. "OU=Users,DC=contoso,DC=com".</summary>
    public string? SearchRoot { get; set; }

    /// <summary>Bu etki alani icin okuma hesabi. Bos ise uygulama kimligi kullanilir.</summary>
    public string? ServiceAccountUserName { get; set; }

    public string? ServiceAccountPassword { get; set; }
}
