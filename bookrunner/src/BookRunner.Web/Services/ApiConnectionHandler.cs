using System.Net;

namespace BookRunner.Web.Services;

/// <summary>
/// API'ye hic ulasilamamasi (baglanti reddi, DNS hatasi, zaman asimi) durumunu
/// anlamli bir <see cref="ApiException"/>'a cevirir.
///
/// <see cref="BookRunnerApiClient"/> yalnizca API'nin donduğu HTTP hata
/// kodlarini (4xx/5xx) <see cref="ApiException"/> olarak isliyordu; API sureci
/// hic ayakta degilse veya erisilemiyorsa firlatilan <see cref="HttpRequestException"/>
/// hicbir yerde yakalanmiyor, dogrudan genel hata sayfasina dusuyordu. Bu
/// handler tum BookRunnerApiClient isteklerinin altinda calisir; boylece tek
/// bir yerde duzeltilince controller'lardaki mevcut
/// <c>catch (ApiException ex)</c> bloklari network hatalarini da yakalar.
/// </summary>
public sealed class ApiConnectionHandler(ILogger<ApiConnectionHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "API'ye baglanilamadi: {Url}", request.RequestUri);
            throw new ApiException(
                HttpStatusCode.ServiceUnavailable,
                "Runbook API servisine ulasilamiyor. API'nin calistigindan ve " +
                "appsettings.json > Api:BaseUrl adresinin dogru oldugundan emin olun.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Kullanicinin istegi iptal etmesiyle (sayfadan ayrilmasi) degil,
            // HttpClient.Timeout'un dolmasiyla olusan iptali ayirt eder.
            logger.LogError(ex, "API istegi zaman asimina ugradi: {Url}", request.RequestUri);
            throw new ApiException(
                HttpStatusCode.GatewayTimeout,
                "Runbook API servisi zamaninda yanit vermedi.");
        }
    }
}
