using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookRunner.Infrastructure.Persistence;

/// <summary>
/// Uygulama acilisinda semayi guncel tutar ve yapilandirmada tanimli AD grup ->
/// rol eslemelerini veritabanina yazar.
/// </summary>
public sealed class DatabaseInitializer(
    BookRunnerDbContext db,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var pending = await db.Database.GetPendingMigrationsAsync(ct);
        var pendingList = pending.ToList();

        if (pendingList.Count > 0)
        {
            logger.LogInformation("{Count} bekleyen migration uygulaniyor...", pendingList.Count);
            await db.Database.MigrateAsync(ct);
        }

        await SeedRoleMappingsAsync(ct);
        await SeedBadgesAsync(ct);
    }

    /// <summary>
    /// Oyunlastirma rozet katalogu kod ile tanimlidir (GamificationService bu
    /// kodlara gore esik kontrolu yapar); burada yalnizca eksik olanlar eklenir,
    /// var olan Ad/Aciklama admin ekranindan degistirilse bile ezilmez.
    /// </summary>
    private async Task SeedBadgesAsync(CancellationToken ct)
    {
        var catalog = new[]
        {
            new Badge { Code = "FIRST_TASK", Name = "Ilk Adim", Description = "Ilk gorevini tamamladin.", Icon = "bi-flag", SortOrder = 1 },
            new Badge { Code = "TASKS_10", Name = "10 Gorev", Description = "10 gorev tamamladin.", Icon = "bi-check2-circle", SortOrder = 2 },
            new Badge { Code = "TASKS_50", Name = "50 Gorev", Description = "50 gorev tamamladin.", Icon = "bi-check2-all", SortOrder = 3 },
            new Badge { Code = "TASKS_100", Name = "100 Gorev", Description = "100 gorev tamamladin.", Icon = "bi-trophy", SortOrder = 4 },
            new Badge { Code = "FIRST_RUNBOOK", Name = "Ilk Runbook", Description = "Sahibi oldugun ilk runbook'u tamamladin.", Icon = "bi-journal-check", SortOrder = 5 },
            new Badge { Code = "COMMENTS_20", Name = "Belgeci", Description = "20 goreve yorum/not biraktin.", Icon = "bi-chat-square-text", SortOrder = 6 }
        };

        var existingCodes = await db.Badges.Select(b => b.Code).ToHashSetAsync(ct);
        var added = 0;

        foreach (var badge in catalog)
        {
            if (existingCodes.Contains(badge.Code))
            {
                continue;
            }

            db.Badges.Add(badge);
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("{Count} rozet katalogdan eklendi.", added);
        }
    }

    /// <summary>
    /// appsettings icindeki "Authorization:TeamRoleMappings" bolumunden gelen
    /// takim -> rol eslemelerini ekler. Var olan kayitlar degistirilmez;
    /// boylece yonetim ekranindan yapilan degisiklikler ezilmez.
    /// </summary>
    private async Task SeedRoleMappingsAsync(CancellationToken ct)
    {
        var configured = configuration
            .GetSection("Authorization:TeamRoleMappings")
            .Get<List<RoleMappingSeed>>() ?? [];

        if (configured.Count == 0)
        {
            return;
        }

        var existing = await db.RoleMappings
            .Select(r => new { r.TeamName, r.Role })
            .ToListAsync(ct);

        var known = existing
            .Select(r => $"{r.TeamName}|{r.Role}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var seed in configured)
        {
            if (string.IsNullOrWhiteSpace(seed.TeamName) || !Enum.TryParse<AppRole>(seed.Role, true, out var role))
            {
                logger.LogWarning("Gecersiz rol eslemesi atlandi: {Team} -> {Role}", seed.TeamName, seed.Role);
                continue;
            }

            if (known.Contains($"{seed.TeamName}|{role}"))
            {
                continue;
            }

            db.RoleMappings.Add(new RoleMapping
            {
                TeamName = seed.TeamName,
                Role = role,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "SYSTEM"
            });

            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("{Count} rol eslemesi yapilandirmadan eklendi.", added);
        }
    }

    private sealed class RoleMappingSeed
    {
        public string? TeamName { get; set; }
        public string? Role { get; set; }
    }
}

/// <summary>Uygulama acilisinda <see cref="DatabaseInitializer"/> calistirma kisayolu.</summary>
public static class DatabaseInitializerExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(ct);
    }
}
