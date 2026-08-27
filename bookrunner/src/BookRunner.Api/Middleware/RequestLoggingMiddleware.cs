using System.Diagnostics;

namespace BookRunner.Api.Middleware;

/// <summary>
/// Her istegi kullanici, sure ve sonuc koduyla loglar. Kurumsal ortamlarda
/// "kim ne zaman hangi ucu cagirdi" sorusunun ilk cevabi buradan gelir.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Saglik ucu ve statik icerik gurultu yaratmasin diye Debug seviyesinde.
            var level = context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug : LogLevel.Information;

            logger.Log(level,
                "{Method} {Path} -> {StatusCode} ({Elapsed} ms) kullanici: {User}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.User.Identity?.Name ?? "anonim");
        }
    }
}
