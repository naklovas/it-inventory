using BookRunner.Domain.Enums;

namespace BookRunner.Infrastructure.Identity;

/// <summary>
/// Rol turetme ayarlari (appsettings: "Authorization").
///
/// Uygulamada iki bagimsiz kavram vardir ve birbirine karistirilmamalidir:
///
///   Rol      - kullanicinin uygulamada <b>ne yapabilecegini</b> belirler ve
///              personel servisinin dondurdugu takim adindan turetilir
///              (bkz. IPersonnelDirectoryService, RoleMapping). Kullanici bazlidir.
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
    /// Tipik kurulumlar:
    ///   Viewer        - kapali kurulum. Yalnizca eslenen takimlardaki kisiler
    ///                   runbook olusturabilir; digerleri sadece okur.
    ///   Contributor   - acik kurulum (onerilen varsayilan). Etki alanindaki
    ///                   herkes kendi runbook'unu acabilir ve sahibi olur;
    ///                   sahiplik yoluyla o runbook'ta gorev ekleyebilir,
    ///                   atama yapabilir, editor belirleyebilir - ama
    ///                   BASKASININ runbook'unu duzenleyemez.
    ///   RunbookAuthor - daha genis acik kurulum. Contributor'a ek olarak
    ///                   baskalarinin runbook'unu da duzenleyebilir/atama
    ///                   yapabilir (RunbookWrite, tum runbook'lar icin gecerli).
    ///   RoleMappings, bu acik kurulumlarda yalnizca yonetici/yazar
    ///   yukseltmesi icin kullanilir.
    /// </summary>
    public AppRole DefaultRole { get; set; } = AppRole.Contributor;
}
