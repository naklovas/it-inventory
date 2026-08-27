using BookRunner.Domain.Enums;

namespace BookRunner.Infrastructure.Identity;

/// <summary>
/// Rol turetme ayarlari (appsettings: "Authorization").
///
/// Uygulamada iki bagimsiz kavram vardir ve birbirine karistirilmamalidir:
///
///   Rol      - kullanicinin uygulamada <b>ne yapabilecegini</b> belirler ve
///              AD grup uyeliginden turetilir. Kullanici bazlidir.
///   Sahiplik - <b>hangi runbook'un</b> sahibi oldugunu belirler. Runbook
///              bazlidir ve runbook'u olusturan kisiye aittir; hicbir gruba
///              veya yapilandirmaya bagli degildir.
/// </summary>
public sealed class RoleOptions
{
    public const string SectionName = "Authorization";

    /// <summary>
    /// <c>RoleMappings</c> icindeki hicbir gruba uymayan kullanicilarin rolu.
    ///
    /// Iki tipik kurulum:
    ///   Viewer        - kapali kurulum. Yalnizca eslenen gruplardaki kisiler
    ///                   runbook olusturabilir; digerleri sadece okur.
    ///   RunbookAuthor - acik kurulum. Etki alanindaki herkes runbook acabilir
    ///                   ve actigi runbook'un sahibi olur. RoleMappings bu
    ///                   durumda yalnizca yonetici yukseltmesi icin kullanilir.
    /// </summary>
    public AppRole DefaultRole { get; set; } = AppRole.Viewer;
}
