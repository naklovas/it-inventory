using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>
/// Giden e-posta kuyrugu (yalnizca izleme/test). Email:Enabled=false iken de
/// her bildirim buraya yazilir; boylece gercek SMTP olmadan hangi olayin kime,
/// ne konuda mail atacagi buradan kontrol edilebilir.
/// </summary>
[ApiController]
[Route("api/email-outbox")]
[Produces("application/json")]
public sealed class EmailOutboxController(IEmailOutboxQueryService outbox) : ControllerBase
{
    /// <summary>Giden e-posta kayitlarini filtreleyerek listeler.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.AuditRead)]
    [ProducesResponseType(typeof(PagedResult<EmailOutboxDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmailOutboxDto>>> List(
        [FromQuery] EmailOutboxFilter filter, CancellationToken ct)
        => Ok(await outbox.ListAsync(filter, ct));
}
