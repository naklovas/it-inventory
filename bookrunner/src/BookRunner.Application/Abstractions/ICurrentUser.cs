using BookRunner.Domain.Enums;

namespace BookRunner.Application.Abstractions;

/// <summary>Istegi yapan Windows kullanicisini is katmanina tasir.</summary>
public interface ICurrentUser
{
    /// <summary>DOMAIN\kullanici. Kimliksiz baglamda (arka plan servisi) "SYSTEM".</summary>
    string UserName { get; }

    string DisplayName { get; }

    /// <summary>AD objectSid. Kimliksiz baglamda null.</summary>
    string? Sid { get; }

    /// <summary>Veritabanindaki AppUser kimligi. Ilk girişte senkronizasyon ile olusur.</summary>
    Guid? UserId { get; }

    /// <summary>Kullanicinin uyesi oldugu AD gruplarinin SID listesi.</summary>
    IReadOnlyCollection<string> GroupSids { get; }

    /// <summary>
    /// Su an gecerli rol. Bir yonetici "test modu" ile kendini baska bir rol
    /// gibi goruntuluyorsa (bkz. RealRole), yetki kontrolleri BUNU kullanir.
    /// </summary>
    AppRole Role { get; }

    /// <summary>
    /// Kullanicinin GERCEK rolu; test modu aktifken bile degismez. Yalnizca
    /// "test modunu kimin acabilecegini" belirlemek icin kullanilir - yetki
    /// kontrollerinde asla Role yerine bu kullanilmaz.
    /// </summary>
    AppRole RealRole { get; }

    /// <summary>
    /// Test modu aktif mi (Role, RealRole'den farkli). Aktifken sahiplik ve
    /// editorluk kontrolleri de (bkz. IRunbookAccess) devre disi kalir - aksi
    /// halde bir yonetici kendi actigi runbook'larda sahiplik yoluyla her
    /// zaman tam yetkili kalir ve "sadece rolum X olsaydi" testi anlamsizlasirdi.
    /// </summary>
    bool IsImpersonating { get; }

    bool IsInRole(AppRole role);

    string? IpAddress { get; }
}
