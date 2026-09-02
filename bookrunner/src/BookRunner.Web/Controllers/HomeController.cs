using System.Diagnostics;
using BookRunner.Web.Models;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Web.Controllers;

/// <summary>Ana ekran ve hata sayfasi.</summary>
public sealed class HomeController(BookRunnerApiClient api, ILogger<HomeController> logger)
    : BaseController(api, logger)
{
    /// <summary>Ozet kartlar, bana atanan gorevler ve son runbook'lar.</summary>
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        try
        {
            var dashboard = await Api.GetDashboardAsync(ct) ?? new();
            return View(await FillAsync(new DashboardViewModel { CurrentUser = currentUser, Dashboard = dashboard }, ct));
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = $"Ozet bilgiler alinamadi: {ex.Message}";
            return View(await FillAsync(new DashboardViewModel { CurrentUser = currentUser }, ct));
        }
    }

    /// <summary>Kullanicinin AD fotografini API uzerinden vekil olarak sunar.</summary>
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var photo = await Api.GetUserPhotoAsync(id, ct);
        return photo is null ? NotFound() : File(photo.Value.Content, photo.Value.ContentType);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            Message = feature?.Error is ApiException apiException
                ? apiException.Message
                : "Islem tamamlanamadi. Lutfen tekrar deneyin veya sistem yoneticinize basvurun."
        });
    }
}
