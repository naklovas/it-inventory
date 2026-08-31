namespace BookRunner.Domain.Entities;

/// <summary>Bir kullanicinin kazandigi rozet.</summary>
public class UserBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid BadgeId { get; set; }

    public DateTimeOffset EarnedAt { get; set; } = DateTimeOffset.UtcNow;

    public Badge Badge { get; set; } = null!;
}
