using System.Security.Principal;

namespace BookRunner.Web.Services;

/// <summary>
/// API'ye giden isteği tarayıcıdaki kullanıcının Windows kimliğiyle gönderir.
///
/// ASP.NET Core, klasik ASP.NET'in aksine, kimliği doğrulanmış kullanıcıyı
/// işletim sistemi seviyesinde otomatik "impersonate" etmez (Kestrel'de de,
/// IIS in-process host'ta da) - bu, IIS'in kendi ayarıyla değil yalnızca kod
/// ile açılabilir. Bu handler olmadan <see cref="BookRunnerApiClient"/>'ın
/// HttpClientHandler.UseDefaultCredentials değeri, isteği yapan tarayıcı
/// kullanıcısı yerine SÜRECİN kendi kimliğini (IIS'te uygulama havuzu hesabı,
/// Windows servisinde servis hesabı) API'ye gönderir.
///
/// Ayrica "test modu" cerezini (bkz. TestModeController) API'nin okuyacagi
/// bir HTTP basligina tasir; API tarafi bu basligi yalnizca GERCEK rolu
/// Yonetici olan kisi icin dikkate alir, bu yuzden cerezi baskasi elle
/// olustursa bile bir yetki kazandirmaz.
/// </summary>
public sealed class WindowsIdentityHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string RoleOverrideCookie = "br-role-override";
    private const string RoleOverrideHeader = "X-Role-Override";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext?.Request.Cookies[RoleOverrideCookie] is { Length: > 0 } roleOverride)
        {
            request.Headers.Add(RoleOverrideHeader, roleOverride);
        }

        if (httpContextAccessor.HttpContext?.User?.Identity is WindowsIdentity { AccessToken.IsInvalid: false } identity)
        {
            return WindowsIdentity.RunImpersonatedAsync(
                identity.AccessToken,
                () => base.SendAsync(request, cancellationToken));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
