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
