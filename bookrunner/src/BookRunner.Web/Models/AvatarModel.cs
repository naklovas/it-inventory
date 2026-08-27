namespace BookRunner.Web.Models;

/// <summary>Avatar rozetinin boyutu.</summary>
public enum AvatarSize
{
    Small,
    Normal,
    Large
}

/// <summary>
/// Kisi/grup rozeti. Fotograf varsa AD fotografi, yoksa bas harfler ve
/// kisiye ozel deterministik renk gosterilir.
/// </summary>
/// <param name="UserId">Fotograf cagrisi icin kullanici kimligi (grup icin null).</param>
/// <param name="DisplayName">Rozetin uzerine gelince gosterilecek ad.</param>
/// <param name="Initials">Bas harfler.</param>
/// <param name="Color">Arka plan rengi (#RRGGBB).</param>
/// <param name="HasPhoto">AD'de fotograf var mi.</param>
/// <param name="Size">Rozet boyutu.</param>
/// <param name="IsGroup">Grup rozeti mi (kare-yuvarlak cizilir).</param>
/// <param name="Subtitle">Ipucu metnine eklenen ikinci satir (unvan/departman).</param>
public sealed record AvatarModel(
    Guid? UserId,
    string DisplayName,
    string Initials,
    string Color,
    bool HasPhoto,
    AvatarSize Size = AvatarSize.Normal,
    bool IsGroup = false,
    string? Subtitle = null);
