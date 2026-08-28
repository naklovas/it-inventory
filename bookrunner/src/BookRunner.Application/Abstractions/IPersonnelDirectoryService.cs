namespace BookRunner.Application.Abstractions;

/// <summary>
/// Kullanicinin takim adini ve profil fotografini kurum ici personel servisinden
/// okur. Rol atamasi bu takim adindan yapilir; AD grup uyeligi sayisindan
/// bagimsizdir (bkz. BookRunnerClaimsTransformation).
/// </summary>
public interface IPersonnelDirectoryService
{
    Task<PersonnelProfile?> GetProfileAsync(string username, CancellationToken ct = default);
}

/// <summary>Personel servisinden donen profil. Thumbnail, varsa JPEG bayt dizisidir.</summary>
public sealed record PersonnelProfile(string Username, string? TeamName, byte[]? Thumbnail);
