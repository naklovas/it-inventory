using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Gorev atamalari ve devir islemleri.</summary>
[ApiController]
[Route("api/tasks/{taskId:guid}/assignments")]
[Produces("application/json")]
public sealed class AssignmentsController(IAssignmentService assignments) : ControllerBase
{
    /// <summary>Gorevin atamalarini listeler.</summary>
    /// <param name="taskId">Gorev kimligi.</param>
    /// <param name="includeInactive">true ise devredilmis/kaldirilmis atamalar da doner.</param>
    /// <param name="ct">Iptal belirteci.</param>
    [HttpGet]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(IReadOnlyList<TaskAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskAssignmentDto>>> List(
        Guid taskId, [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await assignments.ListAsync(taskId, includeInactive, ct));

    /// <summary>Goreve kisi veya AD grubu atar ve ilgililere bildirim gonderir.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.TaskAssign)]
    [ProducesResponseType(typeof(TaskAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskAssignmentDto>> Assign(
        Guid taskId, [FromBody] AssignTaskRequest request, CancellationToken ct)
        => Ok(await assignments.AssignAsync(taskId, request, ct));

    /// <summary>
    /// Gorevi baska bir kisiye veya gruba devreder. Kendisine atanmis kisiler
    /// bu islemi atama yetkisi olmadan da yapabilir.
    /// </summary>
    [HttpPost("handover")]
    [Authorize(Policy = Permissions.TaskExecute)]
    [ProducesResponseType(typeof(TaskAssignmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskAssignmentDto>> Handover(
        Guid taskId, [FromBody] HandoverTaskRequest request, CancellationToken ct)
        => Ok(await assignments.HandoverAsync(taskId, request, ct));

    /// <summary>Atamayi kaldirir (kayit tarihce icin saklanir).</summary>
    [HttpDelete("{assignmentId:guid}")]
    [Authorize(Policy = Permissions.TaskAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid taskId, Guid assignmentId, CancellationToken ct)
    {
        await assignments.RemoveAsync(taskId, assignmentId, ct);
        return NoContent();
    }
}
