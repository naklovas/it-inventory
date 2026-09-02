using BookRunner.Application.Dtos;
using BookRunner.Web.Models;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookRunner.Web.Controllers;

/// <summary>Oyunlastirma: bireysel/takim siralama tablosu ve rozetler.</summary>
public sealed class LeaderboardController(BookRunnerApiClient api, ILogger<LeaderboardController> logger)
    : BaseController(api, logger)
{
    public async Task<IActionResult> Index([FromQuery] LeaderboardPeriod period = LeaderboardPeriod.AllTime, CancellationToken ct = default)
    {
        var currentUser = await GetCurrentUserAsync(ct);

        try
        {
            var users = await Api.GetUserLeaderboardAsync(period, 25, ct) ?? [];
            var teams = await Api.GetTeamLeaderboardAsync(period, ct) ?? [];
            var badges = await Api.GetMyBadgesAsync(ct) ?? [];

            return View(await FillAsync(new LeaderboardViewModel
            {
                CurrentUser = currentUser,
                Period = period,
                Users = users,
                Teams = teams,
                MyBadges = badges
            }, ct));
        }
        catch (ApiException ex)
        {
            TempData["ErrorKind"] = ex.IsInputError ? "input" : "application";
            TempData["Error"] = $"Liderlik tablosu alinamadi: {ex.Message}";
            return View(await FillAsync(new LeaderboardViewModel { CurrentUser = currentUser, Period = period }, ct));
        }
    }
}
