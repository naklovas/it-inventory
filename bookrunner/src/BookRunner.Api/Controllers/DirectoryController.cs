using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>
/// Active Directory kullanici/grup arama ve profil fotografi uclari.
/// Kisi ve grup bilgisi yalnizca AD'den okunur.
/// </summary>
[ApiController]
[Route("api/directory")]
[Produces("application/json")]
public sealed class DirectoryController(
    IDirectorySyncService directory,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Oturum acan kullanicinin profili, rolu ve izinleri.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
    {
        // Profil, oturum acan kullanicinin kimligiyle dogrudan okunur; boylece
        // fotograf, unvan ve departman bilgisi arayuzun sag ust kosesine ulasir.
        var profile = currentUser.UserId.HasValue
            ? await directory.GetPersonAsync(currentUser.UserId.Value, ct)
            : null;

        return Ok(new CurrentUserDto
        {
            Id = currentUser.UserId,
            UserName = currentUser.UserName,
            DisplayName = currentUser.DisplayName,
            Email = profile?.Email,
            Title = profile?.Title,
            Department = profile?.Department,
            Initials = profile?.Initials ?? AvatarHelper.Initials(currentUser.DisplayName),
            AvatarColor = profile?.AvatarColor ?? AvatarHelper.Color(currentUser.Sid ?? currentUser.UserName),
            HasPhoto = profile?.HasPhoto ?? false,
            PhotoUrl = profile?.PhotoUrl,
            Role = DisplayText.Role(currentUser.Role),
            Permissions = Permissions.ForRole(currentUser.Role),
            Groups = currentUser.GroupSids.ToList(),
            IsAdministrator = currentUser.RealRole == AppRole.Administrator,
            IsRoleOverridden = currentUser.Role != currentUser.RealRole
        });
    }

    /// <summary>AD'de kullanici arar (once yerel projeksiyon, yetmezse AD).</summary>
    /// <param name="term">En az 2 karakter: ad, oturum adi veya e-posta.</param>
    /// <param name="take">Donecek en fazla kayit.</param>
    /// <param name="ct">Iptal belirteci.</param>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonSummary>>> SearchUsers(
        [FromQuery] string term, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await directory.SearchUsersAsync(term, take, ct));

    /// <summary>AD'de grup arar.</summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(IReadOnlyList<GroupSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GroupSummary>>> SearchGroups(
        [FromQuery] string term, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await directory.SearchGroupsAsync(term, take, ct));

    /// <summary>Grubun (ic ice uyelikler dahil) uyelerini listeler.</summary>
    [HttpGet("groups/{groupId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonSummary>>> GroupMembers(Guid groupId, CancellationToken ct)
        => Ok(await directory.GetGroupMembersAsync(groupId, ct));

    /// <summary>
    /// Kullanicinin AD'deki profil fotografi. Foto yoksa 404 doner; arayuz bu
    /// durumda bas harflerden olusan avatari gosterir.
    /// </summary>
    [HttpGet("users/{userId:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Photo(Guid userId, CancellationToken ct)
    {
        var photo = await directory.GetUserPhotoAsync(userId, ct);
        return photo is null ? NotFound() : File(photo.Value.Content, photo.Value.ContentType);
    }
}
