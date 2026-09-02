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
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = ex.Message;
            return View(await FillAsync(new AuditViewModel { CurrentUser = currentUser, Filter = filter }, ct));
        }
    }

    /// <summary>
    /// Giden e-posta kuyrugu (test/izleme). Email:Enabled=false iken de her
    /// bildirim burada gorunur; gercek SMTP olmadan tetikleme dogrulanabilir.
    /// </summary>
    public async Task<IActionResult> Emails([FromQuery] EmailOutboxFilter filter, CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        try
        {
            var results = await Api.ListEmailOutboxAsync(filter, ct);
            return View(await FillAsync(new EmailOutboxViewModel
            {
                CurrentUser = currentUser,
                Filter = filter,
                Results = results ?? Application.Common.PagedResult<EmailOutboxDto>.Create([], 1, filter.PageSize, 0)
            }, ct));
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = ex.Message;
            return View(await FillAsync(new EmailOutboxViewModel { CurrentUser = currentUser, Filter = filter }, ct));
        }
    }

    /// <summary>
    /// Takim adi -> rol eslemeleri. Kullanicinin rolu, uyesi oldugu AD gruplarindan
    /// degil, personel servisinin dondurdugu tek bir takim adindan turetilir; bu
    /// ekran o eslemeyi yonetir (Authorization:DefaultRole ise eslesme olmayanlar icin gecerli).
    /// </summary>
    public async Task<IActionResult> Roles(CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        IReadOnlyList<RoleMappingDto> mappings = Array.Empty<RoleMappingDto>();
        try
        {
            mappings = await Api.ListRoleMappingsAsync(ct) ?? mappings;
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = ex.Message;
        }

        return View(await FillAsync(new RoleMappingsViewModel { CurrentUser = currentUser, Mappings = mappings }, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRoleMapping(SaveRoleMappingRequest request, CancellationToken ct)
    {
        if (await TryAsync(async () => { await Api.CreateRoleMappingAsync(request, ct); }, "Esleme eklenemedi"))
        {
            TempData["Success"] = $"'{request.TeamName}' -> {request.Role} eslemesi eklendi.";
        }

        return RedirectToAction(nameof(Roles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRoleMappingActive(Guid id, bool isActive, CancellationToken ct)
    {
        if (await TryAsync(() => Api.SetRoleMappingActiveAsync(id, isActive, ct), "Esleme guncellenemedi"))
        {
            TempData["Success"] = isActive ? "Esleme etkinlestirildi." : "Esleme devre disi birakildi.";
        }

        return RedirectToAction(nameof(Roles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoleMapping(Guid id, CancellationToken ct)
    {
        if (await TryAsync(() => Api.DeleteRoleMappingAsync(id, ct), "Esleme silinemedi"))
        {
            TempData["Success"] = "Esleme silindi.";
        }

        return RedirectToAction(nameof(Roles));
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
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
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
