using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Runbook'larin Excel/PDF olarak disa aktarimi ve Excel'den ice aktarim.</summary>
[ApiController]
[Route("api")]
public sealed class ExportController(IExcelService excel, IPdfService pdf) : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Runbook'u ozet, gorev ve yorum sayfalari halinde Excel'e aktarir.</summary>
    [HttpGet("runbooks/{runbookId:guid}/export/excel")]
    [Authorize(Policy = Permissions.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportRunbookExcel(Guid runbookId, CancellationToken ct)
    {
        var bytes = await excel.ExportRunbookAsync(runbookId, ct);
        return File(bytes, ExcelContentType, $"runbook-{runbookId:N}.xlsx");
    }

    /// <summary>Runbook'u yazdirmaya uygun PDF olarak aktarir.</summary>
    [HttpGet("runbooks/{runbookId:guid}/export/pdf")]
    [Authorize(Policy = Permissions.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportRunbookPdf(Guid runbookId, CancellationToken ct)
    {
        var bytes = await pdf.ExportRunbookAsync(runbookId, ct);
        return File(bytes, "application/pdf", $"runbook-{runbookId:N}.pdf");
    }

    /// <summary>Filtrelenmis runbook listesini Excel'e aktarir.</summary>
    [HttpGet("runbooks/export/excel")]
    [Authorize(Policy = Permissions.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportListExcel([FromQuery] RunbookFilter filter, CancellationToken ct)
    {
        var bytes = await excel.ExportRunbookListAsync(filter, ct);
        return File(bytes, ExcelContentType, $"runbooks-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    /// <summary>Gorev ice aktarimi icin bos Excel sablonunu indirir.</summary>
    [HttpGet("runbooks/import/template")]
    [Authorize(Policy = Permissions.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ImportTemplate()
        => File(excel.CreateImportTemplate(), ExcelContentType, "bookrunner-gorev-sablonu.xlsx");

    /// <summary>
    /// Excel dosyasindaki gorevleri runbook'a aktarir.
    /// <paramref name="commit"/> false ise yalnizca dogrulama yapilir, kayit yazilmaz.
    /// </summary>
    [HttpPost("runbooks/{runbookId:guid}/import/excel")]
    [Authorize(Policy = Permissions.ImportData)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResult>> ImportExcel(
        Guid runbookId, IFormFile file, [FromQuery] bool commit = true, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Dosya bos", Detail = "Yuklenecek bir Excel dosyasi secin." });
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails { Title = "Desteklenmeyen bicim", Detail = "Yalnizca .xlsx dosyalari kabul edilir." });
        }

        await using var stream = file.OpenReadStream();
        return Ok(await excel.ImportTasksAsync(runbookId, stream, commit, ct));
    }
}
