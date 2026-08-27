using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Runbook listeleme, filtreleme ve yasam dongusu islemleri.</summary>
[ApiController]
[Route("api/runbooks")]
[Produces("application/json")]
public sealed class RunbooksController(IRunbookService runbooks) : ControllerBase
{
    /// <summary>Runbook'lari filtreleyerek sayfali biçimde listeler.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(PagedResult<RunbookListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RunbookListItemDto>>> List(
        [FromQuery] RunbookFilter filter, CancellationToken ct)
        => Ok(await runbooks.ListAsync(filter, ct));

    /// <summary>Ana ekran ozet kartlari ve "bana atanan gorevler" listesi.</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await runbooks.GetDashboardAsync(ct));

    /// <summary>Bir runbook'u gorevleri, atamalari ve yorumlariyla birlikte getirir.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(RunbookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunbookDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await runbooks.GetAsync(id, ct));

    /// <summary>Yeni runbook veya sablon olusturur.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.RunbookWrite)]
    [ProducesResponseType(typeof(RunbookDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunbookDetailDto>> Create(
        [FromBody] CreateRunbookRequest request, CancellationToken ct)
    {
        var created = await runbooks.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Runbook basligini, aciklamasini, planini ve durumunu gunceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.RunbookWrite)]
    [ProducesResponseType(typeof(RunbookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RunbookDetailDto>> Update(
        Guid id, [FromBody] UpdateRunbookRequest request, CancellationToken ct)
        => Ok(await runbooks.UpdateAsync(id, request, ct));

    /// <summary>Runbook'u mantiksal olarak siler (gecmis kayitlar korunur).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.RunbookDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await runbooks.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Mevcut runbook'u yeniden kullanilabilir bir sablona donusturur.</summary>
    [HttpPost("{id:guid}/save-as-template")]
    [Authorize(Policy = Permissions.RunbookPublishTemplate)]
    [ProducesResponseType(typeof(RunbookDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RunbookDetailDto>> SaveAsTemplate(
        Guid id, [FromBody] SaveAsTemplateRequest request, CancellationToken ct)
    {
        var template = await runbooks.SaveAsTemplateAsync(id, request.Title, request.Category, ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, template);
    }

    /// <summary>Sablondan yeni bir calisir runbook uretir.</summary>
    [HttpPost("templates/{templateId:guid}/instantiate")]
    [Authorize(Policy = Permissions.RunbookWrite)]
    [ProducesResponseType(typeof(RunbookDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RunbookDetailDto>> CreateFromTemplate(
        Guid templateId, [FromBody] CreateFromTemplateRequest request, CancellationToken ct)
    {
        var created = await runbooks.CreateFromTemplateAsync(templateId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
}

/// <summary>Runbook'u sablona cevirme istegi.</summary>
/// <param name="Title">Sablonun adi.</param>
/// <param name="Category">Sablon kategorisi (opsiyonel).</param>
public sealed record SaveAsTemplateRequest(string Title, string? Category);
