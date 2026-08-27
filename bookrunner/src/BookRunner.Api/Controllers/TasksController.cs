using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Runbook gorevleri: olusturma, guncelleme, durum degistirme, siralama.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class TasksController(ITaskService tasks) : ControllerBase
{
    // Not: Asagidaki uclarda politika, her rolde bulunan "runbook.read" iznidir.
    // Asil yetki karari is katmanindaki IRunbookAccess tarafindan verilir; cunku
    // runbook'un sahibi, rol izni olmasa da kendi runbook'unda her degisikligi
    // yapabilir. Boylece yetki kurali tek yerde toplanir.

    /// <summary>Tek bir gorevi atamalari ve yorumlariyla getirir.</summary>
    [HttpGet("tasks/{taskId:guid}")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(RunbookTaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RunbookTaskDto>> Get(Guid taskId, CancellationToken ct)
        => Ok(await tasks.GetAsync(taskId, ct));

    /// <summary>Runbook'a yeni gorev ekler.</summary>
    [HttpPost("runbooks/{runbookId:guid}/tasks")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(RunbookTaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RunbookTaskDto>> Create(
        Guid runbookId, [FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var created = await tasks.CreateAsync(runbookId, request, ct);
        return CreatedAtAction(nameof(Get), new { taskId = created.Id }, created);
    }

    /// <summary>Gorev detaylarini gunceller.</summary>
    [HttpPut("tasks/{taskId:guid}")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(RunbookTaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RunbookTaskDto>> Update(
        Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateAsync(taskId, request, ct));

    /// <summary>
    /// Gorev durumunu degistirir. Goreve atanmis kisiler kendi gorevlerini
    /// yazma yetkisi olmadan da ilerletebilir.
    /// </summary>
    [HttpPost("tasks/{taskId:guid}/status")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(RunbookTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RunbookTaskDto>> ChangeStatus(
        Guid taskId, [FromBody] ChangeTaskStatusRequest request, CancellationToken ct)
        => Ok(await tasks.ChangeStatusAsync(taskId, request, ct));

    /// <summary>Gorevleri surukle-birak sonrasi yeniden siralar.</summary>
    [HttpPost("runbooks/{runbookId:guid}/tasks/reorder")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reorder(
        Guid runbookId, [FromBody] ReorderTasksRequest request, CancellationToken ct)
    {
        await tasks.ReorderAsync(runbookId, request, ct);
        return NoContent();
    }

    /// <summary>Gorevi mantiksal olarak siler.</summary>
    /// <remarks>Yalnizca yonetici rolu veya runbook sahibi silebilir.</remarks>
    [HttpDelete("tasks/{taskId:guid}")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid taskId, CancellationToken ct)
    {
        await tasks.DeleteAsync(taskId, ct);
        return NoContent();
    }

    /// <summary>Gorevin tarihcesi (arayuzde akordiyon icinde gosterilir).</summary>
    [HttpGet("tasks/{taskId:guid}/history")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(IReadOnlyList<TaskActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskActivityDto>>> History(Guid taskId, CancellationToken ct)
        => Ok(await tasks.GetHistoryAsync(taskId, ct));
}
