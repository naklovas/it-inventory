using BookRunner.Application.Dtos;
using BookRunner.Web.Models;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Web.Controllers;

/// <summary>
/// Oturum acan kullanicinin profilini yukleyip view model'lere gecirir ve
/// API hatalarini kullaniciya anlasilir bicimde gosterir.
/// </summary>
public abstract class BaseController(BookRunnerApiClient api, ILogger logger) : Controller
{
    protected BookRunnerApiClient Api { get; } = api;

    protected ILogger Logger { get; } = logger;

    /// <summary>Kullanici profili istek basina bir kez cekilir.</summary>
    protected async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (HttpContext.Items.TryGetValue("CurrentUser", out var cached) && cached is CurrentUserDto user)
        {
            return user;
        }

        try
        {
            var profile = await Api.GetCurrentUserAsync(ct);
            HttpContext.Items["CurrentUser"] = profile;
            return profile;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Kullanici profili API'den alinamadi.");
            return null;
        }
    }

    /// <summary>View model'e ortak alanlari doldurur.</summary>
    protected async Task<T> FillAsync<T>(T model, CancellationToken ct = default) where T : PageViewModel
    {
        var user = await GetCurrentUserAsync(ct);
        // init-only ortak alani yansitma olmadan doldurabilmek icin ViewData da kullanilir.
        ViewData["CurrentUser"] = user;
        return model;
    }

    /// <summary>API cagrisini calistirir; hata olursa kullaniciya bildirim birakir.</summary>
    protected async Task<bool> TryAsync(Func<Task> action, string errorPrefix)
    {
        try
        {
            await action();
            return true;
        }
        catch (ApiException ex)
        {
            Logger.LogWarning(ex, "{Prefix}", errorPrefix);
            TempData["Error"] = $"{errorPrefix}: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Prefix}", errorPrefix);
            TempData["Error"] = $"{errorPrefix}: beklenmeyen bir hata olustu.";
            return false;
        }
    }

    /// <summary>JSON uclarinda API hatalarini istemciye ayni anlamla iletir.</summary>
    protected async Task<IActionResult> JsonResultAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Json(new { ok = true, data = await action() });
        }
        catch (ApiException ex)
        {
            Response.StatusCode = (int)ex.StatusCode;
            return Json(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Islem basarisiz.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { ok = false, error = "Beklenmeyen bir hata olustu." });
        }
    }
}
