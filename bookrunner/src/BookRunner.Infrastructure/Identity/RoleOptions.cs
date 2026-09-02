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
    /// Personel servisinden gelen TAKIM ADI biliniyor ama <c>RoleMappings</c>
    /// icinde bu takim icin ozel bir esleme YOKSA uygulanacak rol.
    ///
    /// Personel servisi bu kisi icin HICBIR takim adi dondurmuyorsa (kisi
    /// hicbir takimda degil / servis kisiyi tanimiyor) bu ayar devreye
    /// GIRMEZ - o kisi her zaman en dusuk yetkide (Viewer) kalir. Yani
    /// "herkes runbook acabilsin" ilkesi yalnizca TANINAN bir takimin
    /// uyesi olanlar icin gecerlidir.
    ///
    /// Tipik kurulumlar:
    ///   Viewer        - kapali kurulum. Yalnizca ozel eslemesi olan
    ///                   takimlardaki kisiler runbook olusturabilir;
    ///                   takimi bilinen digerleri sadece okur.
    ///   Contributor   - acik kurulum (onerilen varsayilan). Takimi bilinen
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
