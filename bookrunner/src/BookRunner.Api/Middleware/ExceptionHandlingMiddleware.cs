using BookRunner.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Middleware;

/// <summary>
/// Is katmani istisnalarini RFC 7807 ProblemDetails yanitlarina cevirir.
/// Boylece istemci tarafinda tutarli hata isleme yapilabilir.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Yanit baslatildiktan sonra hata olustu; istemciye iletilemedi.");
            throw exception;
        }

        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayit bulunamadi"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Yetkisiz islem"),
            ValidationException => (StatusCodes.Status400BadRequest, "Dogrulama hatasi"),
            BusinessRuleException => (StatusCodes.Status409Conflict, "Islem gerceklestirilemedi"),
            OperationCanceledException => (StatusCodesExtensions.Status499ClientClosedRequest, "Istek iptal edildi"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata olustu")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Islenmeyen hata: {Path}", context.Request.Path);
        }
        else
        {
            logger.LogInformation("Istek reddedildi ({Status}): {Message}", status, exception.Message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Sunucu ici hata detaylari istemciye sizdirilmaz.
            Detail = status >= 500 ? "Islem tamamlanamadi. Lutfen sistem yoneticinize basvurun." : exception.Message,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}

/// <summary>Aslinda standart olmayan ama yaygin kullanilan "istemci baglantiyi kapatti" kodu.</summary>
internal static class StatusCodesExtensions
{
    public const int Status499ClientClosedRequest = 499;
}
