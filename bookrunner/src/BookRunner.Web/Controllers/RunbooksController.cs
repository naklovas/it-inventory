using BookRunner.Application.Dtos;
using BookRunner.Domain.Enums;
using BookRunner.Web.Models;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookRunner.Web.Controllers;

/// <summary>
/// Runbook ekranlari. Sayfa gecisleri klasik MVC, ekran ici etkilesimler
/// (gorev ekleme, atama, yorum, tarihce) ise bu denetleyicideki JSON uclari
/// uzerinden yapilir. Tarayici hicbir zaman API'ye dogrudan gitmez; boylece
/// Kerberos ve CORS karmasikligi tek noktada toplanir.
/// </summary>
public sealed class RunbooksController(
    BookRunnerApiClient api,
    IOptions<ApiOptions> apiOptions,
    ILogger<RunbooksController> logger) : BaseController(api, logger)
{
    private readonly ApiOptions _apiOptions = apiOptions.Value;

    // ------------------------------------------------------------ liste/detay

    /// <summary>Runbook listesi; arama, durum, etiket ve tarih filtreleri.</summary>
    public async Task<IActionResult> Index([FromQuery] RunbookFilter filter, CancellationToken ct)
    {
        filter = filter with { IsTemplate = filter.IsTemplate ?? false };

        var currentUser = await GetCurrentUserAsync(ct);
        var emptyResults = Application.Common.PagedResult<RunbookListItemDto>.Create([], 1, filter.PageSize, 0);

        IReadOnlyList<string> seyirNames;
        try
        {
            seyirNames = await Api.GetSeyirNamesAsync(ct) ?? Array.Empty<string>();
        }
        catch (ApiException)
        {
            seyirNames = Array.Empty<string>();
        }

        try
        {
            var results = await Api.ListRunbooksAsync(filter, ct);
            return View(await FillAsync(new RunbookListViewModel
            {
                CurrentUser = currentUser,
                Filter = filter,
                Results = results ?? emptyResults,
                SeyirNames = seyirNames
            }, ct));
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = ex.Message;
            return View(await FillAsync(new RunbookListViewModel
            {
                CurrentUser = currentUser,
                Filter = filter,
                Results = emptyResults,
                SeyirNames = seyirNames
            }, ct));
        }
    }

    /// <summary>Runbook detayi: renkli gorev barlari, atamalar, yorumlar, tarihce.</summary>
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        RunbookDetailDto? runbook;
        try
        {
            runbook = await Api.GetRunbookAsync(id, ct);
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        if (runbook is null)
        {
            return NotFound();
        }

        var scripts = Array.Empty<ScriptDto>() as IReadOnlyList<ScriptDto>;
        try
        {
            scripts = await Api.ListScriptsAsync(id, ct) ?? scripts;
        }
        catch (ApiException)
        {
            // Script yetkisi olmayan kullanicilar icin bu bolum gizlenir.
        }

        return View(await FillAsync(new RunbookDetailViewModel
        {
            CurrentUser = currentUser,
            Runbook = runbook,
            Scripts = scripts,
            HubUrl = string.IsNullOrWhiteSpace(_apiOptions.HubUrl)
                ? $"{_apiOptions.BaseUrl.TrimEnd('/')}/hubs/runbook"
                : _apiOptions.HubUrl
        }, ct));
    }

    // ------------------------------------------------------------ olustur/duzenle

    [HttpGet]
    public async Task<IActionResult> Create(bool isTemplate, CancellationToken ct)
    {
        var form = new RunbookFormViewModel
        {
            CurrentUser = await GetCurrentUserAsync(ct),
            IsTemplate = isTemplate
        };
        await PopulateSeyirNamesAsync(form, ct);
        return View(await FillAsync(form, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RunbookFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSeyirNamesAsync(form, ct);
            return View(await FillAsync(form, ct));
        }

        RunbookDetailDto? created = null;
        var ok = await TryAsync(async () =>
        {
            created = await Api.CreateRunbookAsync(new CreateRunbookRequest
            {
                Title = form.Title,
                Description = form.Description,
                IsTemplate = form.IsTemplate,
                TemplateCategory = form.TemplateCategory,
                SeyirName = form.SeyirName,
                PlannedStart = ToOffset(form.PlannedStart),
                PlannedEnd = ToOffset(form.PlannedEnd),
                ServiceManagerWorkItemId = form.ServiceManagerWorkItemId,
                Tags = form.Tags
            }, ct);
        }, "Runbook olusturulamadi");

        if (!ok || created is null)
        {
            await PopulateSeyirNamesAsync(form, ct);
            return View(await FillAsync(form, ct));
        }

        TempData["Success"] = $"{created.Code} olusturuldu.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var runbook = await Api.GetRunbookAsync(id, ct);
        if (runbook is null)
        {
            return NotFound();
        }

        var form = new RunbookFormViewModel
        {
            CurrentUser = await GetCurrentUserAsync(ct),
            Id = runbook.Id,
            Code = runbook.Code,
            Title = runbook.Title,
            Description = runbook.Description,
            Status = runbook.Status,
            IsTemplate = runbook.IsTemplate,
            TemplateCategory = runbook.TemplateCategory,
            SeyirName = runbook.SeyirName,
            PlannedStart = runbook.PlannedStart?.LocalDateTime,
            PlannedEnd = runbook.PlannedEnd?.LocalDateTime,
            ServiceManagerWorkItemId = runbook.ServiceManagerWorkItemId,
            TagsText = string.Join(", ", runbook.Tags),
            RowVersion = runbook.RowVersion
        };
        await PopulateSeyirNamesAsync(form, ct);
        return View(await FillAsync(form, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RunbookFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSeyirNamesAsync(form, ct);
            return View(await FillAsync(form, ct));
        }

        var ok = await TryAsync(() => Api.UpdateRunbookAsync(id, new UpdateRunbookRequest
        {
            Title = form.Title,
            Description = form.Description,
            Status = form.Status,
            TemplateCategory = form.TemplateCategory,
            SeyirName = form.SeyirName,
            PlannedStart = ToOffset(form.PlannedStart),
            PlannedEnd = ToOffset(form.PlannedEnd),
            ServiceManagerWorkItemId = form.ServiceManagerWorkItemId,
            Tags = form.Tags,
            RowVersion = form.RowVersion
        }, ct), "Runbook guncellenemedi");

        if (!ok)
        {
            await PopulateSeyirNamesAsync(form, ct);
            return View(await FillAsync(form, ct));
        }

        TempData["Success"] = "Runbook guncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (await TryAsync(() => Api.DeleteRunbookAsync(id, ct), "Runbook silinemedi"))
        {
            TempData["Success"] = "Runbook silindi.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Mevcut runbook'u sablona cevirir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAsTemplate(Guid id, string title, string? category, CancellationToken ct)
    {
        RunbookDetailDto? template = null;
        var ok = await TryAsync(async () => template = await Api.SaveAsTemplateAsync(id, title, category, ct),
            "Sablon olusturulamadi");

        if (ok && template is not null)
        {
            TempData["Success"] = $"{template.Code} sablonu olusturuldu.";
            return RedirectToAction(nameof(Details), new { id = template.Id });
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------ disa/ice aktarim

    public async Task<IActionResult> ExportExcel(Guid id, CancellationToken ct)
    {
        var file = await Api.ExportRunbookExcelAsync(id, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken ct)
    {
        var file = await Api.ExportRunbookPdfAsync(id, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> ExportList([FromQuery] RunbookFilter filter, CancellationToken ct)
    {
        var file = await Api.ExportRunbookListExcelAsync(filter, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> ImportTemplate(CancellationToken ct)
    {
        var file = await Api.GetImportTemplateAsync(ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Excel'den gorev ice aktarir. Once dogrulama, sonra kayit yapilir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Import(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ErrorKind"] = "input";
            TempData["Error"] = "Lutfen bir Excel dosyasi secin.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await Api.ImportTasksAsync(id, stream, file.FileName, commit: true, ct);

            if (result is null)
            {
                TempData["ErrorKind"] = "application";
                TempData["Error"] = "Ice aktarim sonucu alinamadi.";
            }
            else if (result.Committed)
            {
                TempData["Success"] = $"{result.ImportedRows} gorev ice aktarildi.";
            }
            else
            {
                // Satir bazli hatalar (gecersiz tarih, bos baslik, runbook araligi
                // disi tarih vb.) her zaman GIRIS HATASIDIR - dosyanin icerigi
                // duzeltilmelidir, uygulamada bir ariza yoktur.
                var details = string.Join(" | ", result.Errors.Take(5).Select(e => $"Satir {e.Row} ({e.Column}): {e.Message}"));
                TempData["ErrorKind"] = "input";
                TempData["Error"] = result.Errors.Count > 0
                    ? $"Ice aktarim yapilmadi. {details}"
                    : "Ice aktarilacak gecerli satir bulunamadi.";
            }
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = $"Ice aktarim basarisiz: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------------- JSON uclari

    /// <summary>Runbook'a yeni gorev ekler.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddTask(Guid id, [FromBody] CreateTaskRequest request, CancellationToken ct)
        => JsonResultAsync(() => Api.CreateTaskAsync(id, request, ct));

    /// <summary>Gorev detaylarini gunceller.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken ct)
        => JsonResultAsync(() => Api.UpdateTaskAsync(taskId, request, ct));

    /// <summary>Gorev durumunu degistirir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeTaskStatus(Guid taskId, RunbookTaskStatus status, string? note, CancellationToken ct)
        => JsonResultAsync(() => Api.ChangeTaskStatusAsync(taskId, new ChangeTaskStatusRequest { Status = status, Note = note }, ct));

    /// <summary>Gorevleri surukle-birak sonrasi siralar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReorderTasks(Guid id, [FromBody] ReorderTasksRequest request, CancellationToken ct)
        => JsonResultAsync<object?>(async () =>
        {
            await Api.ReorderTasksAsync(id, request, ct);
            return null;
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteTask(Guid taskId, CancellationToken ct)
        => JsonResultAsync<object?>(async () =>
        {
            await Api.DeleteTaskAsync(taskId, ct);
            return null;
        });

    /// <summary>Goreve tiklaninca acilan akordiyon tarihcesi.</summary>
    [HttpGet]
    public Task<IActionResult> TaskHistory(Guid taskId, CancellationToken ct)
        => JsonResultAsync(() => Api.GetTaskHistoryAsync(taskId, ct));

    /// <summary>Goreve kisi veya takim atar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Assign(Guid taskId, [FromBody] AssignTaskRequest request, CancellationToken ct)
        => JsonResultAsync(() => Api.AssignAsync(taskId, request, ct));

    /// <summary>
    /// Ayni goreve birden fazla kisi/takim atar; hepsi icin TEK bir bildirim
    /// e-postasi gider. Yeni gorev formunda birden fazla sorumlu secildiginde
    /// kullanilir.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AssignBatch(Guid taskId, [FromBody] List<AssignTaskRequest> requests, CancellationToken ct)
        => JsonResultAsync(() => Api.AssignManyAsync(taskId, requests, ct));

    /// <summary>Runbook'a "Editor" ekler. Yalnizca runbook sahibi cagirabilir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddCollaborator(Guid id, [FromBody] AddRunbookCollaboratorRequest request, CancellationToken ct)
        => JsonResultAsync(() => Api.AddCollaboratorAsync(id, request, ct));

    /// <summary>Editor kaldirir. Yalnizca runbook sahibi cagirabilir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveCollaborator(Guid id, Guid collaboratorId, CancellationToken ct)
        => JsonResultAsync<object?>(async () =>
        {
            await Api.RemoveCollaboratorAsync(id, collaboratorId, ct);
            return null;
        });

    /// <summary>Gorevi baska kisiye/gruba devreder.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Handover(Guid taskId, [FromBody] HandoverTaskRequest request, CancellationToken ct)
        => JsonResultAsync(() => Api.HandoverAsync(taskId, request, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveAssignment(Guid taskId, Guid assignmentId, CancellationToken ct)
        => JsonResultAsync<object?>(async () =>
        {
            await Api.RemoveAssignmentAsync(taskId, assignmentId, ct);
            return null;
        });

    /// <summary>Goreve yorum ekler.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddComment(Guid taskId, [FromBody] CreateCommentRequest request, CancellationToken ct)
        => JsonResultAsync(() => Api.AddCommentAsync(taskId, request, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct)
        => JsonResultAsync<object?>(async () =>
        {
            await Api.DeleteCommentAsync(commentId, ct);
            return null;
        });

    /// <summary>Atama kutusundaki kisi arama kutusu icin AD sorgusu.</summary>
    [HttpGet]
    public Task<IActionResult> SearchUsers(string term, CancellationToken ct)
        => JsonResultAsync(() => Api.SearchUsersAsync(term, 15, ct));

    /// <summary>Atama kutusundaki takim arama kutusu.</summary>
    [HttpGet]
    public Task<IActionResult> SearchGroups(string term, CancellationToken ct)
        => JsonResultAsync(() => Api.SearchGroupsAsync(term, 15, ct));

    /// <summary>Grup rozetine tiklaninca uyelerini gosterir.</summary>
    [HttpGet]
    public Task<IActionResult> GroupMembers(Guid groupId, CancellationToken ct)
        => JsonResultAsync(() => Api.GetGroupMembersAsync(groupId, ct));

    /// <summary>Service Manager kayit numarasi arama kutusu.</summary>
    [HttpGet]
    public Task<IActionResult> SearchWorkItems(string term, CancellationToken ct)
        => JsonResultAsync(() => Api.SearchWorkItemsAsync(term, 15, ct));

    /// <summary>Goreve bagli CSX script'ini calistirir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RunScript(Guid scriptId, Guid? taskId, CancellationToken ct)
        => JsonResultAsync(() => Api.RunScriptAsync(scriptId, new RunScriptRequest { TaskId = taskId }, ct));

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Local)) : null;

    /// <summary>Seyir alanindaki otomatik tamamlama listesini doldurur; API'ye ulasilamazsa form yine de gosterilir.</summary>
    private async Task PopulateSeyirNamesAsync(RunbookFormViewModel form, CancellationToken ct)
    {
        try
        {
            form.SeyirNames = await Api.GetSeyirNamesAsync(ct) ?? [];
        }
        catch (ApiException)
        {
            form.SeyirNames = [];
        }
    }
}
