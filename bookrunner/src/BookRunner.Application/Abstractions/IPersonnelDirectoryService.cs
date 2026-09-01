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
    /// Sirketteki tum ekiplerin adlarini toplu olarak dondurur (personel servisi
    /// "/api/takimlar" ucu). Henuz BookRunner'da hic kimsesi giris yapmamis
    /// takimlarin da atama arama kutusunda gorunebilmesi icin kullanilir.
    /// </summary>
    Task<IReadOnlyList<string>> GetTeamNamesAsync(CancellationToken ct = default);
}

/// <summary>Personel servisinden donen profil. Thumbnail, varsa JPEG bayt dizisidir.</summary>
public sealed record PersonnelProfile(string Username, string? TeamName, byte[]? Thumbnail);
