using MailRelay.Service.Data;

namespace MailRelay.Service.Security;

// /api/mail/* uc noktalarini korur. X-Api-Key header'i dbo.ClientApplications tablosunda
// aktif (Enabled=1) bir kayitla eslesmelidir. Eslesen uygulama HttpContext.Items uzerinden
// endpoint'e tasinir (ClientApplicationId loglamak/yetki kontrolu icin kullanilir).
public sealed class ClientApiKeyFilter : IEndpointFilter
{
    public const string HttpContextItemKey = "ClientApplication";

    private readonly ClientApplicationRepository _repository;

    public ClientApiKeyFilter(ClientApplicationRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues) || string.IsNullOrWhiteSpace(apiKeyValues))
            return Results.Unauthorized();

        var app = await _repository.FindByApiKeyAsync(apiKeyValues.ToString(), httpContext.RequestAborted);
        if (app is null)
            return Results.Unauthorized();

        httpContext.Items[HttpContextItemKey] = app;
        return await next(context);
    }
}
