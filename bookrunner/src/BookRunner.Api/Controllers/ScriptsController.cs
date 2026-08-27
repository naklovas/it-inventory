using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Runbook adimlarina baglanan Roslyn (CSX) script'leri.</summary>
[ApiController]
[Route("api/scripts")]
[Produces("application/json")]
public sealed class ScriptsController(IScriptService scripts) : ControllerBase
{
    /// <summary>Script'leri listeler. <paramref name="runbookId"/> verilirse o runbook'a ozel olanlar da doner.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.ScriptExecute)]
    [ProducesResponseType(typeof(IReadOnlyList<ScriptDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScriptDto>>> List(
        [FromQuery] Guid? runbookId, CancellationToken ct)
        => Ok(await scripts.ListAsync(runbookId, ct));

    /// <summary>Tek bir script'i kaynak koduyla getirir.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ScriptExecute)]
    [ProducesResponseType(typeof(ScriptDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScriptDto>> Get(Guid id, CancellationToken ct)
        => Ok(await scripts.GetAsync(id, ct));

    /// <summary>Yeni script olusturur.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.ScriptManage)]
    [ProducesResponseType(typeof(ScriptDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ScriptDto>> Create([FromBody] SaveScriptRequest request, CancellationToken ct)
    {
        var created = await scripts.SaveAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Mevcut script'i gunceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ScriptManage)]
    [ProducesResponseType(typeof(ScriptDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScriptDto>> Update(
        Guid id, [FromBody] SaveScriptRequest request, CancellationToken ct)
        => Ok(await scripts.SaveAsync(id, request, ct));

    /// <summary>Script'i siler.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.ScriptManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await scripts.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Script'i calistirir; sonuc ve cikti satirlari audit'e yazilir.</summary>
    [HttpPost("{id:guid}/run")]
    [Authorize(Policy = Permissions.ScriptExecute)]
    [ProducesResponseType(typeof(ScriptRunResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScriptRunResult>> Run(
        Guid id, [FromBody] RunScriptRequest request, CancellationToken ct)
        => Ok(await scripts.RunAsync(id, request, ct));
}
