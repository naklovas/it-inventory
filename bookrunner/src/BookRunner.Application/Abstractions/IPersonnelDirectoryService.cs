namespace BookRunner.Application.Abstractions;

/// <summary>
/// Kullanicinin takim adini ve profil fotografini kurum ici personel servisinden
/// okur. Rol atamasi bu takim adindan yapilir; AD grup uyeligi sayisindan
/// bagimsizdir (bkz. BookRunnerClaimsTransformation).
/// </summary>
public interface IPersonnelDirectoryService
{
    Task<PersonnelProfile?> GetProfileAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Sirketteki tum ekipleri, uye ad-soyad listeleriyle birlikte toplu olarak
    /// dondurur (personel servisi "/api/takimlar" ucu). Henuz BookRunner'da hic
    /// kimsesi giris yapmamis takimlarin da atama arama kutusunda gorunebilmesi
    /// ve gorev bildirimlerinin tum uyelere gidebilmesi icin kullanilir.
    /// </summary>
    Task<IReadOnlyList<PersonnelTeamSummary>> GetTeamsAsync(CancellationToken ct = default);
}

/// <summary>Personel servisinden donen profil. Thumbnail, varsa JPEG bayt dizisidir.</summary>
public sealed record PersonnelProfile(string Username, string? TeamName, byte[]? Thumbnail);

/// <summary>
/// "/api/takimlar" ucundan gelen tek bir ekip. MemberNames kullanici adi degil
/// ad-soyad icerir (yoneticiler + kadrolu + danisman birlestirilmis) - AD'de
/// tek ve kesin eslesme bulunursa o kisi ekibe baglanir, birden fazla veya
/// hic eslesme yoksa atlanir (yanlis kisiyi eklememek icin).
/// </summary>
public sealed record PersonnelTeamSummary(string Name, IReadOnlyList<string> MemberNames);
