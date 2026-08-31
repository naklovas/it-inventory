using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Application.Services;

/// <summary>
/// Gorev/runbook/yorum olaylarindan puan ve rozet uretir. Amac katilim ve
/// sahiplenmeyi tesvik etmek; oncelik agirlikli puanlama sayesinde onemsiz
/// gorev acip puan toplamanin bir faydasi olmaz (bkz. GamificationOptions).
/// Ayni gorev/runbook icin ayni tur puan yalnizca bir kez verilir - durum
/// ileri geri degistirilerek puan biriktirilemez.
/// </summary>
public sealed class GamificationService(
    IAppDbContext db,
    IOptions<GamificationOptions> options,
    ILogger<GamificationService> logger) : IGamificationService
{
    private static readonly (string Code, int Threshold)[] TaskMilestones =
    [
        ("FIRST_TASK", 1),
        ("TASKS_10", 10),
        ("TASKS_50", 50),
        ("TASKS_100", 100)
    ];

    private readonly GamificationOptions _options = options.Value;

    public async Task OnTaskClosedAsync(RunbookTask task, Guid actorUserId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (task.Status == RunbookTaskStatus.Completed)
        {
            if (await db.GamificationEvents.AnyAsync(
                    e => e.RunbookTaskId == task.Id && e.EventType == GamificationEventType.TaskCompleted, ct))
            {
                return;
            }

            var multiplier = task.Priority switch
            {
                TaskPriority.Critical => _options.CriticalPriorityMultiplier,
                TaskPriority.High => _options.HighPriorityMultiplier,
                _ => 1.0
            };

            AddEvent(actorUserId, GamificationEventType.TaskCompleted,
                (int)Math.Round(_options.TaskCompletionPoints * multiplier), task.RunbookId, task.Id);

            if (task.PlannedEnd is { } plannedEnd && task.ActualEnd is { } actualEnd && actualEnd <= plannedEnd)
            {
                AddEvent(actorUserId, GamificationEventType.TaskOnTimeBonus,
                    _options.OnTimeBonusPoints, task.RunbookId, task.Id);
            }

            await db.SaveChangesAsync(ct);
            await CheckTaskMilestoneBadgesAsync(actorUserId, ct);
            await db.SaveChangesAsync(ct);
        }
        else if (task.Status == RunbookTaskStatus.Failed)
        {
            if (await db.GamificationEvents.AnyAsync(
                    e => e.RunbookTaskId == task.Id && e.EventType == GamificationEventType.TaskFailedPenalty, ct))
            {
                return;
            }

            AddEvent(actorUserId, GamificationEventType.TaskFailedPenalty,
                _options.TaskFailedPenaltyPoints, task.RunbookId, task.Id);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task OnRunbookCompletedAsync(Runbook runbook, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (await db.GamificationEvents.AnyAsync(
                e => e.RunbookId == runbook.Id && e.EventType == GamificationEventType.RunbookCompleted, ct))
        {
            return;
        }

        AddEvent(runbook.OwnerUserId, GamificationEventType.RunbookCompleted,
            _options.RunbookCompletionPoints, runbook.Id, null);

        await db.SaveChangesAsync(ct);

        var completedRunbooks = await db.GamificationEvents.CountAsync(
            e => e.UserId == runbook.OwnerUserId && e.EventType == GamificationEventType.RunbookCompleted, ct);
        if (completedRunbooks >= 1)
        {
            await AwardBadgeIfMissingAsync(runbook.OwnerUserId, "FIRST_RUNBOOK", ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task OnCommentAddedAsync(Guid authorUserId, Guid taskId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var runbookId = await db.Tasks.AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => (Guid?)t.RunbookId)
            .FirstOrDefaultAsync(ct);

        AddEvent(authorUserId, GamificationEventType.CommentAdded, _options.CommentPoints, runbookId, taskId);
        await db.SaveChangesAsync(ct);

        var commentCount = await db.GamificationEvents.CountAsync(
            e => e.UserId == authorUserId && e.EventType == GamificationEventType.CommentAdded, ct);
        if (commentCount >= 20)
        {
            await AwardBadgeIfMissingAsync(authorUserId, "COMMENTS_20", ct);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetUserLeaderboardAsync(
        LeaderboardPeriod period, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var since = PeriodStart(period);

        var totals = await db.GamificationEvents.AsNoTracking()
            .Where(e => since == null || e.CreatedAt >= since)
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Points = g.Sum(e => e.Points),
                CompletedTasks = g.Count(e => e.EventType == GamificationEventType.TaskCompleted)
            })
            .OrderByDescending(x => x.Points)
            .Take(take)
            .ToListAsync(ct);

        if (totals.Count == 0)
        {
            return [];
        }

        var userIds = totals.Select(t => t.UserId).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var badgeCounts = await db.UserBadges.AsNoTracking()
            .Where(ub => userIds.Contains(ub.UserId))
            .GroupBy(ub => ub.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var rank = 0;
        var results = new List<LeaderboardEntryDto>();
        foreach (var row in totals)
        {
            if (!users.TryGetValue(row.UserId, out var user))
            {
                continue;
            }

            rank++;
            results.Add(new LeaderboardEntryDto
            {
                Rank = rank,
                Person = user.ToSummary(),
                TeamName = user.TeamName,
                Points = row.Points,
                CompletedTaskCount = row.CompletedTasks,
                BadgeCount = badgeCounts.GetValueOrDefault(row.UserId)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<TeamLeaderboardEntryDto>> GetTeamLeaderboardAsync(
        LeaderboardPeriod period, CancellationToken ct = default)
    {
        var since = PeriodStart(period);

        var pointsByUser = await db.GamificationEvents.AsNoTracking()
            .Where(e => since == null || e.CreatedAt >= since)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(e => e.Points) })
            .ToListAsync(ct);

        if (pointsByUser.Count == 0)
        {
            return [];
        }

        var userIds = pointsByUser.Select(p => p.UserId).ToList();
        var teamNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.TeamName, ct);

        var rank = 0;
        return pointsByUser
            .Where(p => !string.IsNullOrWhiteSpace(teamNames.GetValueOrDefault(p.UserId)))
            .GroupBy(p => teamNames[p.UserId]!)
            .Select(g => new TeamLeaderboardEntryDto
            {
                TeamName = g.Key,
                MemberCount = g.Select(x => x.UserId).Distinct().Count(),
                TotalPoints = g.Sum(x => x.Points),
                AveragePointsPerMember = Math.Round(g.Average(x => x.Points), 1)
            })
            .OrderByDescending(t => t.AveragePointsPerMember)
            .Select(t => t with { Rank = ++rank })
            .ToList();
    }

    public async Task<IReadOnlyList<BadgeDto>> GetUserBadgesAsync(Guid userId, CancellationToken ct = default)
    {
        var earned = await db.UserBadges.AsNoTracking()
            .Where(ub => ub.UserId == userId)
            .ToDictionaryAsync(ub => ub.BadgeId, ub => ub.EarnedAt, ct);

        var badges = await db.Badges.AsNoTracking().OrderBy(b => b.SortOrder).ToListAsync(ct);

        return badges.Select(b => new BadgeDto
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            Description = b.Description,
            Icon = b.Icon,
            Earned = earned.ContainsKey(b.Id),
            EarnedAt = earned.GetValueOrDefault(b.Id)
        }).ToList();
    }

    private void AddEvent(Guid userId, GamificationEventType type, int points, Guid? runbookId, Guid? taskId)
        => db.GamificationEvents.Add(new GamificationEvent
        {
            UserId = userId,
            EventType = type,
            Points = points,
            RunbookId = runbookId,
            RunbookTaskId = taskId
        });

    private async Task CheckTaskMilestoneBadgesAsync(Guid userId, CancellationToken ct)
    {
        var completedCount = await db.GamificationEvents.CountAsync(
            e => e.UserId == userId && e.EventType == GamificationEventType.TaskCompleted, ct);

        foreach (var (code, threshold) in TaskMilestones)
        {
            if (completedCount >= threshold)
            {
                await AwardBadgeIfMissingAsync(userId, code, ct);
            }
        }
    }

    private async Task AwardBadgeIfMissingAsync(Guid userId, string badgeCode, CancellationToken ct)
    {
        var badge = await db.Badges.FirstOrDefaultAsync(b => b.Code == badgeCode, ct);
        if (badge is null)
        {
            logger.LogWarning("Rozet katalogda bulunamadi: {Code}", badgeCode);
            return;
        }

        if (await db.UserBadges.AnyAsync(ub => ub.UserId == userId && ub.BadgeId == badge.Id, ct))
        {
            return;
        }

        db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = badge.Id });
    }

    private static DateTimeOffset? PeriodStart(LeaderboardPeriod period) => period switch
    {
        LeaderboardPeriod.ThisMonth => new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero),
        _ => null
    };
}
