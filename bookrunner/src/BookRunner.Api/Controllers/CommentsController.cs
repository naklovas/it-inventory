using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Gorev yorumlari.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class CommentsController(ICommentService comments) : ControllerBase
{
    // Not: Asagidaki uclarda politika, her rolde bulunan "runbook.read" iznidir.
    // Asil yetki karari is katmanindaki IRunbookAccess tarafindan verilir; cunku
    // runbook'un sahibi, rol izni olmasa da kendi runbook'unda her degisikligi
    // yapabilir. Boylece yetki kurali tek yerde toplanir.

    /// <summary>Gorevin yorumlarini kronolojik sirayla listeler.</summary>
    [HttpGet("tasks/{taskId:guid}/comments")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(IReadOnlyList<TaskCommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskCommentDto>>> List(Guid taskId, CancellationToken ct)
        => Ok(await comments.ListAsync(taskId, ct));

    /// <summary>Goreve yorum ekler; anilan kisilere e-posta gonderilir.</summary>
    [HttpPost("tasks/{taskId:guid}/comments")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(TaskCommentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskCommentDto>> Add(
        Guid taskId, [FromBody] CreateCommentRequest request, CancellationToken ct)
        => Ok(await comments.AddAsync(taskId, request, ct));

    /// <summary>Kendi yorumunu siler (yoneticiler tum yorumlari silebilir).</summary>
    [HttpDelete("comments/{commentId:guid}")]
    [Authorize(Policy = Permissions.TaskComment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid commentId, CancellationToken ct)
    {
        await comments.DeleteAsync(commentId, ct);
        return NoContent();
    }
}
