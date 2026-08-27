using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using BookRunner.Api.Identity;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Security;
using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Identity;
using BookRunner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BookRunner.Api.Authorization;

/// <summary>
/// Windows kimligini uygulama profiline donusturur: kullaniciyi AD'den senkronize
/// eder, grup uyeliklerini cozer ve AD grubu -> rol eslemesinden izin claim'lerini
/// ekler. Sonuc kisa sureli onbellege alinir; her istekte AD'ye gidilmez.
/// </summary>
public sealed class BookRunnerClaimsTransformation(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    IOptions<RoleOptions> roleOptions,
    ILogger<BookRunnerClaimsTransformation> logger) : IClaimsTransformation
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly RoleOptions _roleOptions = roleOptions.Value;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var name = principal.Identity?.Name;
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(name))
        {
            return principal;
        }

        // Zaten donusturulmus bir principal tekrar islenmez.
        if (principal.HasClaim(c => c.Type == HttpCurrentUser.RoleClaim))
        {
            return principal;
        }

        var profile = await GetProfileAsync(name);
        if (profile is null)
        {
            return principal;
        }

        var identity = new ClaimsIdentity();

        if (profile.UserId.HasValue)
        {
            identity.AddClaim(new Claim(HttpCurrentUser.UserIdClaim, profile.UserId.Value.ToString()));
        }

        identity.AddClaim(new Claim(HttpCurrentUser.RoleClaim, profile.Role.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, profile.DisplayName));
        identity.AddClaim(new Claim("displayName", profile.DisplayName));

        if (!string.IsNullOrWhiteSpace(profile.Email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, profile.Email));
        }

        foreach (var groupSid in profile.GroupSids)
        {
            identity.AddClaim(new Claim(HttpCurrentUser.GroupSidClaim, groupSid));
        }

        foreach (var permission in Permissions.ForRole(profile.Role))
        {
            identity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }

        principal.AddIdentity(identity);
        return principal;
    }

    private async Task<UserProfile?> GetProfileAsync(string userName)
    {
        var cacheKey = $"profile:{userName.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out UserProfile? cached))
        {
            return cached;
        }

        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        try
        {
            var directorySync = provider.GetRequiredService<IDirectorySyncService>();
            var db = provider.GetRequiredService<BookRunnerDbContext>();

            var user = await directorySync.EnsureUserBySamAccountNameAsync(userName);
            if (user is null)
            {
                // AD'de karsiligi olmayan hesap: yapilandirilan varsayilan rol degil,
                // her zaman en dusuk yetki verilir.
                logger.LogWarning("{User} Active Directory'de bulunamadi; en dusuk yetkiyle devam ediliyor.", userName);
                return new UserProfile(null, userName, null, AppRole.Viewer, []);
            }

            await directorySync.SyncUserGroupsAsync(user.Id);

            var groupSids = await db.UserGroups
                .Where(ug => ug.UserId == user.Id)
                .Select(ug => ug.Group.Sid)
                .ToListAsync();

            var role = await ResolveRoleAsync(db, groupSids, _roleOptions.DefaultRole);

            user.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            var profile = new UserProfile(user.Id, user.DisplayName, user.Email, role, groupSids);
            cache.Set(cacheKey, profile, CacheDuration);
            return profile;
        }
        catch (Exception ex)
        {
            // Kimlik zenginlestirme basarisiz olsa da istek reddedilmez; kullanici
            // en dusuk yetkiyle devam eder ve durum loglanir. Hata durumunda
            // varsayilan rol uygulanmaz: gecici bir AD arizasi kimseye fazladan
            // yetki vermemeli.
            logger.LogError(ex, "{User} icin profil olusturulamadi.", userName);
            return new UserProfile(null, userName, null, AppRole.Viewer, []);
        }
    }

    /// <summary>
    /// Kullanicinin gruplarina karsilik gelen en yuksek rolu bulur.
    /// Hicbir esleme tutmazsa yapilandirmadaki varsayilan rol uygulanir;
    /// boylece "etki alanindaki herkes runbook acabilsin" kurulumu tek ayarla
    /// mumkun olur.
    /// </summary>
    private static async Task<AppRole> ResolveRoleAsync(
        BookRunnerDbContext db, List<string> groupSids, AppRole defaultRole)
    {
        if (groupSids.Count == 0)
        {
            return defaultRole;
        }

        var roles = await db.RoleMappings
            .Where(r => r.IsActive && groupSids.Contains(r.GroupSid))
            .Select(r => r.Role)
            .ToListAsync();

        // Esleme varsa en yuksegi, yoksa varsayilan rol gecerlidir.
        return roles.Count == 0 ? defaultRole : roles.Max();
    }

    private sealed record UserProfile(
        Guid? UserId,
        string DisplayName,
        string? Email,
        AppRole Role,
        IReadOnlyList<string> GroupSids);
}
