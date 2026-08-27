using BookRunner.Application.Dtos;
using BookRunner.Web.Models;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Web.Controllers;

/// <summary>Audit trail ve entegrasyon durumu ekranlari (yonetici).</summary>
public sealed class AdminController(BookRunnerApiClient api, ILogger<AdminController> logger)
    : BaseController(api, logger)
{
    /// <summary>Audit kayitlarini filtreleyerek listeler.</summary>
    public async Task<IActionResult> Audit([FromQuery] AuditFilter filter, CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        try
        {
            var results = await Api.ListAuditAsync(filter, ct);
            return View(await FillAsync(new AuditViewModel
            {
                CurrentUser = currentUser,
                Filter = filter,
                Results = results ?? Application.Common.PagedResult<AuditLogDto>.Create([], 1, filter.PageSize, 0)
            }, ct));
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return View(await FillAsync(new AuditViewModel { CurrentUser = currentUser, Filter = filter }, ct));
        }
    }

    /// <summary>Service Manager baglantisi ve script kutuphanesi durumu.</summary>
    public async Task<IActionResult> Integrations(CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        ServiceManagerHealth? health = null;
        IReadOnlyList<ScriptDto> scripts = Array.Empty<ScriptDto>();

        try
        {
            health = await Api.GetServiceManagerHealthAsync(ct);
            scripts = await Api.ListScriptsAsync(null, ct) ?? scripts;
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return View(await FillAsync(new AdminViewModel
        {
            CurrentUser = currentUser,
            ServiceManager = health,
            Scripts = scripts
        }, ct));
    }
}
