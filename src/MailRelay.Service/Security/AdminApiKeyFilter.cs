using System.Security.Cryptography;
using System.Text;
using MailRelay.Service.Options;
using Microsoft.Extensions.Options;

namespace MailRelay.Service.Security;

// /api/admin/* uc noktalarini korur. Basit, paylasimli anahtar tabanli koruma - internal ag
// ya da bir ters proxy (reverse proxy) arkasinda calistirilmasi onerilir. Sabit sureli
// karsilastirma (FixedTimeEquals) zamanlama (timing) saldirilarina karsi kullanilir.
public sealed class AdminApiKeyFilter : IEndpointFilter
{
    private readonly AdminOptions _options;
    private readonly ILogger<AdminApiKeyFilter> _logger;

    public AdminApiKeyFilter(IOptions<AdminOptions> options, ILogger<AdminApiKeyFilter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogError("Admin:ApiKey yapilandirilmamis - tum admin uc noktalari kapatildi.");
            return ValueTask.FromResult<object?>(Results.Problem("Admin API anahtari yapilandirilmamis.", statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue("X-Admin-Key", out var provided) || !IsValidKey(provided.ToString()))
            return ValueTask.FromResult<object?>(Results.Unauthorized());

        return next(context);
    }

    private bool IsValidKey(string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
