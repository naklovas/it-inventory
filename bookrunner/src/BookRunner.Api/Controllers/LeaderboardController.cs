using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Api.Controllers;

/// <summary>
/// Oyunlastirma siralama tablolari ve rozetler. Herkese acik (kimlik dogrulanmis
/// her kullanici gorebilir); ozel bir izin gerektirmez.
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Produces("application/json")]
public sealed class LeaderboardController(
    IGamificationService gamification,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Bireysel siralama tablosu.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaderboardEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> Users(
        [FromQuery] LeaderboardPeriod period = LeaderboardPeriod.AllTime,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
        => Ok(await gamification.GetUserLeaderboardAsync(period, take, ct));

    /// <summary>Takim bazli siralama tablosu (kisi basi ortalama puana gore).</summary>
    [HttpGet("teams")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamLeaderboardEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamLeaderboardEntryDto>>> Teams(
        [FromQuery] LeaderboardPeriod period = LeaderboardPeriod.AllTime,
        CancellationToken ct = default)
        => Ok(await gamification.GetTeamLeaderboardAsync(period, ct));

    /// <summary>Oturum acan kullanicinin rozet durumu (kazanilmis/kazanilmamis hepsi).</summary>
    [HttpGet("my-badges")]
    [ProducesResponseType(typeof(IReadOnlyList<BadgeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BadgeDto>>> MyBadges(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Ok(Array.Empty<BadgeDto>());
        }

        return Ok(await gamification.GetUserBadgesAsync(userId, ct));
    }
}
