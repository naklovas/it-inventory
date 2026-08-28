using System.Security.Cryptography;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookRunner.Application.Services;

/// <summary>
/// AD, kullanici/grup bilgisinin tek dogru kaynagidir. Bu servis AD kayitlarini
/// yerel tablolara yansitir; boylece raporlar ve gorev atamalari AD'ye her seferinde
/// gitmeden calisir, AD erisilemedigi anlarda da uygulama okunabilir kalir.
/// </summary>
public sealed class DirectorySyncService(
    IAppDbContext db,
    IDirectoryService directory,
    IPersonnelDirectoryService personnelDirectory,
    ILogger<DirectorySyncService> logger) : IDirectorySyncService
{
    /// <summary>Bu sureden eski kayitlar bir sonraki erisimde AD'den tazelenir.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);

    public async Task<AppUser> EnsureUserBySidAsync(string sid, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Sid == sid, ct);
        if (user is not null && !NeedsRefresh(user.LastSyncedAt))
        {
            return user;
        }

        var adUser = await directory.FindUserBySidAsync(sid, ct);
        if (adUser is null)
        {
            if (user is not null)
            {
                // AD'de bulunamadi: kayit korunur ama pasife cekilir; gecmis atamalar bozulmaz.
                user.IsActive = false;
                user.LastSyncedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return user;
            }

            throw new NotFoundException("Active Directory kullanicisi", sid);
        }

        return await UpsertAsync(adUser, ct);
    }

    public async Task<AppUser?> EnsureUserBySamAccountNameAsync(string samAccountName, CancellationToken ct = default)
    {
        var normalized = NormalizeAccountName(samAccountName);

        var user = await db.Users.FirstOrDefaultAsync(u => u.SamAccountName == normalized, ct);
        if (user is not null && !NeedsRefresh(user.LastSyncedAt))
        {
            return user;
        }

        // Cok domainli ormanlarda dogru etki alanina once sorulabilmesi icin
        // "DOMAIN\\kullanici" biciminde gelen deger oldugu gibi iletilir.
        var adUser = await directory.FindUserBySamAccountNameAsync(samAccountName, ct);
        if (adUser is null)
        {
            return user;
        }

        return await UpsertAsync(adUser, ct);
    }

    public async Task<AppGroup> EnsureGroupBySidAsync(string sid, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Sid == sid, ct);
        if (group is not null && !NeedsRefresh(group.LastSyncedAt))
        {
            return group;
        }

        var adGroup = await directory.FindGroupBySidAsync(sid, ct);
        if (adGroup is null)
        {
            if (group is not null)
            {
                group.IsActive = false;
                group.LastSyncedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return group;
            }

            throw new NotFoundException("Active Directory grubu", sid);
        }

        if (group is null)
        {
            group = new AppGroup { Sid = adGroup.Sid, AvatarColor = AvatarHelper.Color(adGroup.Sid) };
            db.Groups.Add(group);
        }

        var cleanedGroupDisplayName = AvatarHelper.StripTrailingAnnotation(adGroup.DisplayName);

        group.Name = adGroup.Name;
        group.DisplayName = string.IsNullOrWhiteSpace(cleanedGroupDisplayName) ? adGroup.Name : cleanedGroupDisplayName;
        group.Description = adGroup.Description;
        group.Email = adGroup.Email;
        group.DistinguishedName = adGroup.DistinguishedName;
        group.IsActive = true;
        group.LastSyncedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return group;
    }

    public async Task SyncUserGroupsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Kullanici", userId);

        IReadOnlyList<string> groupSids;
        try
        {
            groupSids = await directory.GetUserGroupSidsAsync(user.SamAccountName, ct);
        }
        catch (Exception ex)
        {
            // AD gecici olarak erisilemezse mevcut uyelik projeksiyonu korunur.
            logger.LogWarning(ex, "{User} icin AD grup uyelikleri okunamadi.", user.SamAccountName);
            return;
        }

        var existing = await db.UserGroups
            .Include(ug => ug.Group)
            .Where(ug => ug.UserId == userId)
            .ToListAsync(ct);

        var existingSids = existing.Select(e => e.Group.Sid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incoming = groupSids.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stale in existing.Where(e => !incoming.Contains(e.Group.Sid)))
        {
            db.UserGroups.Remove(stale);
        }

        foreach (var sid in incoming.Where(s => !existingSids.Contains(s)))
        {
            AppGroup group;
            try
            {
                group = await EnsureGroupBySidAsync(sid, ct);
            }
            catch (NotFoundException)
            {
                // Yerlesik/yerel gruplar (orn. "Everyone") AD'de bulunmaz; atlanir.
                continue;
            }

            db.UserGroups.Add(new AppUserGroup
            {
                UserId = userId,
                GroupId = group.Id,
                SyncedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PersonSummary>> SearchUsersAsync(string term, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
        {
            return Array.Empty<PersonSummary>();
        }

        term = term.Trim();

        var local = await db.Users
            .Where(u => u.IsActive &&
                        (EF.Functions.Like(u.DisplayName, $"%{term}%") ||
                         EF.Functions.Like(u.SamAccountName, $"%{term}%") ||
                         (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"))))
            .OrderBy(u => u.DisplayName)
            .Take(take)
            .ToListAsync(ct);

        var results = local.Select(u => u.ToSummary()).ToList();
        if (results.Count >= take)
        {
            return results;
        }

        // Yerel projeksiyon yetmediyse AD'ye sorulur ve bulunanlar kalici hale getirilir.
        try
        {
            var known = local.Select(u => u.Sid).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var adUser in await directory.SearchUsersAsync(term, take - results.Count, ct))
            {
                if (known.Contains(adUser.Sid))
                {
                    continue;
                }

                var saved = await UpsertAsync(adUser, ct);
                results.Add(saved.ToSummary());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AD kullanici aramasi basarisiz oldu; yalnizca yerel sonuclar donuluyor.");
        }

        return results;
    }

    public async Task<IReadOnlyList<GroupSummary>> SearchGroupsAsync(string term, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
        {
            return Array.Empty<GroupSummary>();
        }

        term = term.Trim();

        var local = await db.Groups
            .Where(g => g.IsActive &&
                        (EF.Functions.Like(g.Name, $"%{term}%") || EF.Functions.Like(g.DisplayName, $"%{term}%")))
            .OrderBy(g => g.Name)
            .Take(take)
            .ToListAsync(ct);

        var results = local.Select(g => g.ToSummary()).ToList();
        if (results.Count >= take)
        {
            return results;
        }

        try
        {
            var known = local.Select(g => g.Sid).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var adGroup in await directory.SearchGroupsAsync(term, take - results.Count, ct))
            {
                if (known.Contains(adGroup.Sid))
                {
                    continue;
                }

                var saved = await EnsureGroupBySidAsync(adGroup.Sid, ct);
                results.Add(saved.ToSummary());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AD grup aramasi basarisiz oldu; yalnizca yerel sonuclar donuluyor.");
        }

        return results;
    }

    public async Task<PersonSummary?> GetPersonAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.ToSummary();
    }

    public async Task<(byte[] Content, string ContentType)?> GetUserPhotoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        if (user.Photo is { Length: > 0 })
        {
            return (user.Photo, user.PhotoContentType ?? "image/jpeg");
        }

        try
        {
            var photo = await directory.GetUserPhotoAsync(user.Sid, ct);
            if (photo is not { Length: > 0 })
            {
                return null;
            }

            user.Photo = photo;
            user.PhotoContentType = "image/jpeg";
            user.PhotoHash = Convert.ToHexString(SHA256.HashData(photo));
            await db.SaveChangesAsync(ct);
            return (photo, user.PhotoContentType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{User} icin AD fotografi okunamadi.", user.SamAccountName);
            return null;
        }
    }

    public async Task<IReadOnlyList<PersonSummary>> GetGroupMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new NotFoundException("Grup", groupId);

        var members = new List<PersonSummary>();
        try
        {
            foreach (var adUser in await directory.GetGroupMembersAsync(group.Sid, ct))
            {
                var saved = await UpsertAsync(adUser, ct);
                members.Add(saved.ToSummary());
            }

            return members;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Group} grubunun uyeleri AD'den okunamadi; yerel projeksiyon kullaniliyor.", group.Name);
        }

        var local = await db.UserGroups
            .Where(ug => ug.GroupId == groupId)
            .Select(ug => ug.User)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        return local.Select(u => u.ToSummary()).ToList();
    }

    /// <summary>AD'den okunan kullaniciyi yerel tabloya ekler veya gunceller.</summary>
    private async Task<AppUser> UpsertAsync(DirectoryUser adUser, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Sid == adUser.Sid, ct);
        if (user is null)
        {
            user = new AppUser { Sid = adUser.Sid, AvatarColor = AvatarHelper.Color(adUser.Sid) };
            db.Users.Add(user);
        }

        var cleanedDisplayName = AvatarHelper.StripTrailingAnnotation(adUser.DisplayName);

        user.SamAccountName = adUser.SamAccountName;
        user.UserPrincipalName = adUser.UserPrincipalName;
        user.DisplayName = string.IsNullOrWhiteSpace(cleanedDisplayName) ? adUser.SamAccountName : cleanedDisplayName;
        user.Email = adUser.Email;
        user.Title = adUser.Title;
        user.Department = adUser.Department;
        user.Company = adUser.Company;
        user.OfficePhone = adUser.OfficePhone;
        user.MobilePhone = adUser.MobilePhone;
        user.ManagerDistinguishedName = adUser.ManagerDistinguishedName;
        user.DistinguishedName = adUser.DistinguishedName;
        user.Initials = AvatarHelper.Initials(user.DisplayName);
        user.IsActive = adUser.IsActive;
        user.LastSyncedAt = DateTimeOffset.UtcNow;

        await ApplyPhotoAsync(user, adUser, ct);

        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Fotograf, AD'nin kendi thumbnailPhoto/jpegPhoto oznitelikleri yerine
    /// oncelikle personel servisinden alinir (bkz. IPersonnelDirectoryService);
    /// bircok kurulumda AD tarafinda foto hic tutulmaz. Bu, AD'den okunan HER
    /// kullanici icin gecerlidir (atama/arama sonuclari dahil) - yalnizca
    /// oturum acan kisiye ozel degildir. Personel servisi bos donerse AD
    /// fotografina geri dusulur.
    /// </summary>
    private async Task ApplyPhotoAsync(AppUser user, DirectoryUser adUser, CancellationToken ct)
    {
        var personnel = await personnelDirectory.GetProfileAsync(adUser.SamAccountName, ct);
        var photo = personnel?.Thumbnail ?? adUser.Photo;

        if (photo is not { Length: > 0 })
        {
            return;
        }

        var hash = Convert.ToHexString(SHA256.HashData(photo));
        if (user.PhotoHash == hash)
        {
            return;
        }

        user.Photo = photo;
        user.PhotoContentType = "image/jpeg";
        user.PhotoHash = hash;
    }

    private static bool NeedsRefresh(DateTimeOffset? lastSyncedAt)
        => lastSyncedAt is null || DateTimeOffset.UtcNow - lastSyncedAt.Value > RefreshInterval;

    /// <summary>"DOMAIN\ali" veya "ali@contoso.com" girdisini sAMAccountName'e indirger.</summary>
    private static string NormalizeAccountName(string value)
    {
        var name = value.Trim();
        var slash = name.LastIndexOf('\\');
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        var at = name.IndexOf('@');
        return at > 0 ? name[..at] : name;
    }
}
