using BookRunner.Application.Dtos;
using BookRunner.Web.Models;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Web.Controllers;

/// <summary>
/// Sablon ekranlari. Sablonlar runbook ile ayni yapiyi kullanir; farki
/// calistirilmamalari ve yeni runbook uretmek icin kullanilmalaridir.
/// </summary>
public sealed class TemplatesController(BookRunnerApiClient api, ILogger<TemplatesController> logger)
    : BaseController(api, logger)
{
    /// <summary>Sablon kutuphanesi.</summary>
    public async Task<IActionResult> Index([FromQuery] RunbookFilter filter, CancellationToken ct)
    {
        filter = filter with { IsTemplate = true };

        var results = await Api.ListRunbooksAsync(filter, ct);

        return View(await FillAsync(new RunbookListViewModel
        {
            CurrentUser = await GetCurrentUserAsync(ct),
            Filter = filter,
            TemplatesView = true,
            Results = results ?? Application.Common.PagedResult<RunbookListItemDto>.Create([], 1, filter.PageSize, 0)
        }, ct));
    }

    /// <summary>Sablondan yeni runbook uretir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Use(
        Guid id, string title, DateTime? plannedStart, DateTime? plannedEnd,
        string? serviceManagerWorkItemId, bool copyAssignments, CancellationToken ct)
    {
        RunbookDetailDto? created = null;

        var ok = await TryAsync(async () => created = await Api.CreateFromTemplateAsync(id, new CreateFromTemplateRequest
        {
            Title = title,
            PlannedStart = ToOffset(plannedStart),
            PlannedEnd = ToOffset(plannedEnd),
            ServiceManagerWorkItemId = serviceManagerWorkItemId,
            CopyAssignments = copyAssignments
        }, ct), "Sablondan runbook uretilemedi");

        if (ok && created is not null)
        {
            TempData["Success"] = $"{created.Code} sablondan olusturuldu.";
            return RedirectToAction("Details", "Runbooks", new { id = created.Id });
        }

        return RedirectToAction(nameof(Index));
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Local)) : null;
}
