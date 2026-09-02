using BookRunner.Domain.Enums;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Web.Controllers;

/// <summary>
/// "Test modu": yalnizca GERCEK rolu Yonetici olan biri, izin modelini test
/// etmek icin kendini baska bir rol gibi goruntuleyebilir - surekli baska
/// birine "sen giris yap da bakalim" demek yerine. Buradaki cerez, API'ye
/// giden her istekte WindowsIdentityHandler tarafindan bir HTTP basligina
/// tasinir; API tarafi bu basligi yalnizca GERCEK rolu Yonetici olan kisi
/// icin dikkate alir (bkz. BookRunnerClaimsTransformation.ResolveRoleOverride) -
/// yani bu cerezi baskasi elle olusturmaya calissa bile hicbir yetki kazanamaz.
/// </summary>
public sealed class TestModeController(BookRunnerApiClient api, ILogger<TestModeController> logger)
    : BaseController(api, logger)
{
    public const string CookieName = "br-role-override";

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(AppRole role, string? returnUrl, CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);
        if (currentUser?.IsAdministrator == true)
        {
            Response.Cookies.Append(CookieName, role.ToString(), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear(string? returnUrl)
    {
        Response.Cookies.Delete(CookieName);
        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
