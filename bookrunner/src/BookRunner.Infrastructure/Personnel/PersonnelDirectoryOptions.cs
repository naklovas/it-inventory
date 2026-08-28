namespace BookRunner.Infrastructure.Personnel;

/// <summary>
/// Kurum ici personel bilgi servisi ayarlari (appsettings: "PersonnelDirectory").
/// Kullanicinin takim adi ve fotografi AD grup uyeliginden degil bu servisten
/// alinir; boylece rol ataması bir kullanicinin uyesi oldugu AD grubu sayisindan
/// bagimsiz olur.
/// </summary>
public sealed class PersonnelDirectoryOptions
{
    public const string SectionName = "PersonnelDirectory";

    public bool Enabled { get; set; }

    /// <summary>Orn. "http://doku:5406".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// {0} yerine kullanici adi (samAccountName) konur, oncesinde URL-encode edilir.
    /// Orn. "/api/personel/{0}".
    /// </summary>
    public string LookupPathTemplate { get; set; } = "/api/personel/{0}";

    public int TimeoutSeconds { get; set; } = 5;
}
