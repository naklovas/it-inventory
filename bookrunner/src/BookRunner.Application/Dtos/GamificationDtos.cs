namespace BookRunner.Application.Dtos;

/// <summary>Liderlik tablosunun donem secimi.</summary>
public enum LeaderboardPeriod
{
    AllTime = 0,
    ThisMonth = 1
}

/// <summary>Bireysel siralama tablosundaki bir satir.</summary>
public sealed record LeaderboardEntryDto
{
    public int Rank { get; init; }
    public required PersonSummary Person { get; init; }
    public string? TeamName { get; init; }
    public int Points { get; init; }
    public int CompletedTaskCount { get; init; }
    public int BadgeCount { get; init; }
}

/// <summary>Takim bazli siralama tablosundaki bir satir.</summary>
public sealed record TeamLeaderboardEntryDto
{
    public int Rank { get; init; }
    public required string TeamName { get; init; }
    public int MemberCount { get; init; }
    public int TotalPoints { get; init; }
    /// <summary>Kalabalik bir takim otomatik one cikmasin diye kisi basi ortalama puan.</summary>
    public double AveragePointsPerMember { get; init; }
}

/// <summary>Kazanilmis/kazanilmamis rozet listesi icin ozet.</summary>
public sealed record BadgeDto
{
    public Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public bool Earned { get; init; }
    public DateTimeOffset? EarnedAt { get; init; }
}
