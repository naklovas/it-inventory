using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>
/// System Center Service Manager kayitlarina veritabani seviyesinde,
/// salt-okunur erisim. Runbook'lar bu kayitlarla iliskilendirilir.
/// </summary>
[ApiController]
[Route("api/service-manager")]
[Produces("application/json")]
public sealed class ServiceManagerController(IServiceManagerReader serviceManager) : ControllerBase
{
    /// <summary>Kayit numarasi veya basliga gore is kaydi arar.</summary>
    [HttpGet("work-items")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceManagerWorkItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceManagerWorkItem>>> Search(
        [FromQuery] string term, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await serviceManager.SearchWorkItemsAsync(term, take, ct));

    /// <summary>Tek bir is kaydinin detayini getirir.</summary>
    [HttpGet("work-items/{id}")]
    [Authorize(Policy = Permissions.RunbookRead)]
    [ProducesResponseType(typeof(ServiceManagerWorkItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceManagerWorkItem>> Get(string id, CancellationToken ct)
    {
        var item = await serviceManager.GetWorkItemAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>SCSM baglantisinin durumunu dondurur (yonetim ekrani icin).</summary>
    [HttpGet("health")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(ServiceManagerHealth), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceManagerHealth>> Health(CancellationToken ct)
        => Ok(await serviceManager.CheckHealthAsync(ct));
}
