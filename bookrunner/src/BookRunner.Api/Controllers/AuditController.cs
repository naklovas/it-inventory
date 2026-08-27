using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>Audit trail sorgulama (yalnizca yonetici rolu).</summary>
[ApiController]
[Route("api/audit")]
[Produces("application/json")]
public sealed class AuditController(IAuditQueryService audit) : ControllerBase
{
    /// <summary>Audit kayitlarini filtreleyerek listeler.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.AuditRead)]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] AuditFilter filter, CancellationToken ct)
        => Ok(await audit.ListAsync(filter, ct));
}
