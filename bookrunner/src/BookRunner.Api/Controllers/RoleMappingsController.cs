using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>
/// Takim adi -> rol eslemeleri. Kullanicinin rolu, uyesi oldugu AD gruplarindan
/// degil, personel servisinin dondurdugu takim adindan turetilir; burada
/// eslesme yoksa Authorization:DefaultRole (appsettings) uygulanir.
/// </summary>
[ApiController]
[Route("api/role-mappings")]
[Produces("application/json")]
public sealed class RoleMappingsController(IRoleMappingService roleMappings) : ControllerBase
{
    /// <summary>Tum takim-rol eslemelerini listeler.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleMappingDto>>> List(CancellationToken ct)
        => Ok(await roleMappings.ListAsync(ct));

    /// <summary>Yeni bir takim-rol eslemesi olusturur.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(RoleMappingDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RoleMappingDto>> Create([FromBody] SaveRoleMappingRequest request, CancellationToken ct)
    {
        var created = await roleMappings.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), created);
    }

    /// <summary>Eslemeyi etkinlestirir/devre disi birakir (silmeden).</summary>
    [HttpPost("{id:guid}/active")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive, CancellationToken ct)
    {
        await roleMappings.SetActiveAsync(id, isActive, ct);
        return NoContent();
    }

    /// <summary>Eslemeyi kalici olarak siler.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await roleMappings.DeleteAsync(id, ct);
        return NoContent();
    }
}
