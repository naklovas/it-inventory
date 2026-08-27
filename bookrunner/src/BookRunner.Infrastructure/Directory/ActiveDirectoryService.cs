using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Principal;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Directory;

/// <summary>
/// Active Directory'yi salt-okunur kullanan dizin servisi. Kullanici, grup, uyelik
/// ve fotograf bilgisi buradan gelir; uygulama AD'ye hicbir sey yazmaz.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ActiveDirectoryService(
    IOptions<ActiveDirectoryOptions> options,
    IMemoryCache cache,
    ILogger<ActiveDirectoryService> logger) : IDirectoryService
{
    private readonly ActiveDirectoryOptions _options = options.Value;

    /// <summary>Kullanici nesnelerinden okunacak nitelikler.</summary>
    private static readonly string[] UserProperties =
    [
        "objectSid", "sAMAccountName", "userPrincipalName", "displayName", "givenName", "sn",
        "mail", "title", "department", "company", "telephoneNumber", "mobile", "manager",
        "distinguishedName", "userAccountControl"
    ];

    private static readonly string[] GroupProperties =
    [
        "objectSid", "sAMAccountName", "displayName", "description", "mail", "distinguishedName"
    ];

    public Task<IReadOnlyList<DirectoryUser>> SearchUsersAsync(string term, int take, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult<IReadOnlyList<DirectoryUser>>(Array.Empty<DirectoryUser>());
        }

        var escaped = EscapeLdapFilter(term.Trim());
        var filter = $"(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))" +
                     $"(|(displayName=*{escaped}*)(sAMAccountName=*{escaped}*)(mail=*{escaped}*)(givenName=*{escaped}*)(sn=*{escaped}*)))";

        return Task.FromResult(CacheOrQuery(
            $"ad:users:{escaped}:{take}",
            () => Search(filter, UserProperties, Math.Min(take, _options.MaxSearchResults), ReadUser)));
    }

    public Task<IReadOnlyList<DirectoryGroup>> SearchGroupsAsync(string term, int take, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult<IReadOnlyList<DirectoryGroup>>(Array.Empty<DirectoryGroup>());
        }

        var escaped = EscapeLdapFilter(term.Trim());
        var filter = $"(&(objectCategory=group)(|(sAMAccountName=*{escaped}*)(displayName=*{escaped}*)))";

        return Task.FromResult(CacheOrQuery(
            $"ad:groups:{escaped}:{take}",
            () => Search(filter, GroupProperties, Math.Min(take, _options.MaxSearchResults), ReadGroup)));
    }

    public Task<DirectoryUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(samAccountName))
        {
            return Task.FromResult<DirectoryUser?>(null);
        }

        var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdapFilter(samAccountName)}))";
        return Task.FromResult(CacheOrQuery(
            $"ad:user:sam:{samAccountName.ToLowerInvariant()}",
            () => Search(filter, UserProperties, 1, ReadUser).FirstOrDefault()));
    }

    public Task<DirectoryUser?> FindUserBySidAsync(string sid, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(sid))
        {
            return Task.FromResult<DirectoryUser?>(null);
        }

        var filter = $"(&(objectCategory=person)(objectClass=user)(objectSid={sid}))";
        return Task.FromResult(CacheOrQuery(
            $"ad:user:sid:{sid}",
            () => Search(filter, UserProperties, 1, ReadUser).FirstOrDefault()));
    }

    public Task<DirectoryGroup?> FindGroupBySidAsync(string sid, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(sid))
        {
            return Task.FromResult<DirectoryGroup?>(null);
        }

        var filter = $"(&(objectCategory=group)(objectSid={sid}))";
        return Task.FromResult(CacheOrQuery(
            $"ad:group:sid:{sid}",
            () => Search(filter, GroupProperties, 1, ReadGroup).FirstOrDefault()));
    }

    public Task<IReadOnlyList<string>> GetUserGroupSidsAsync(string samAccountName, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(samAccountName))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return Task.FromResult(CacheOrQuery($"ad:groupsof:{samAccountName.ToLowerInvariant()}", () =>
        {
            using var context = CreateContext();
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);
            if (user is null)
            {
                return (IReadOnlyList<string>)Array.Empty<string>();
            }

            var sids = new List<string>();

            // GetAuthorizationGroups ic ice grup uyeliklerini de cozer; bir uyelik
            // cozulemezse (orn. guven iliskisi kopuksa) o kayit atlanir.
            using var groups = user.GetAuthorizationGroups();
            var enumerator = groups.GetEnumerator();
            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }

                    if (enumerator.Current?.Sid is { } sid)
                    {
                        sids.Add(sid.Value);
                    }
                }
                catch (Exception ex) when (ex is PrincipalOperationException or NoMatchingPrincipalException)
                {
                    logger.LogDebug(ex, "{User} icin bir grup uyeligi cozulemedi, atlaniyor.", samAccountName);
                }
            }

            return sids;
        }));
    }

    public Task<IReadOnlyList<DirectoryUser>> GetGroupMembersAsync(string groupSid, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(groupSid))
        {
            return Task.FromResult<IReadOnlyList<DirectoryUser>>(Array.Empty<DirectoryUser>());
        }

        return Task.FromResult(CacheOrQuery($"ad:members:{groupSid}", () =>
        {
            using var context = CreateContext();
            using var group = GroupPrincipal.FindByIdentity(context, IdentityType.Sid, groupSid);
            if (group is null)
            {
                return (IReadOnlyList<DirectoryUser>)Array.Empty<DirectoryUser>();
            }

            var members = new List<DirectoryUser>();

            // recursive: true -> ic ice gruplardaki kisiler de bildirim alsin.
            foreach (var principal in group.GetMembers(recursive: true))
            {
                using (principal)
                {
                    if (principal is not UserPrincipal userPrincipal)
                    {
                        continue;
                    }

                    if (userPrincipal.GetUnderlyingObject() is DirectoryEntry entry)
                    {
                        var user = ReadUser(entry.Properties);
                        if (user is not null)
                        {
                            members.Add(user);
                        }
                    }
                }
            }

            return members;
        }));
    }

    public Task<byte[]?> GetUserPhotoAsync(string sid, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(sid))
        {
            return Task.FromResult<byte[]?>(null);
        }

        try
        {
            using var root = CreateSearchRoot();
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectCategory=person)(objectClass=user)(objectSid={sid}))",
                SizeLimit = 1
            };

            foreach (var attribute in _options.PhotoAttributes)
            {
                searcher.PropertiesToLoad.Add(attribute);
            }

            using var results = searcher.FindAll();
            foreach (SearchResult result in results)
            {
                foreach (var attribute in _options.PhotoAttributes)
                {
                    if (result.Properties[attribute].Count > 0 &&
                        result.Properties[attribute][0] is byte[] { Length: > 0 } photo)
                    {
                        return Task.FromResult<byte[]?>(photo);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Sid} icin AD fotografi okunamadi.", sid);
        }

        return Task.FromResult<byte[]?>(null);
    }

    /// <summary>Sorguyu onbellekten karsilar; AD hatalarinda bos sonuc dondurup uygulamayi ayakta tutar.</summary>
    private T CacheOrQuery<T>(string cacheKey, Func<T> query) where T : class?
    {
        if (cache.TryGetValue(cacheKey, out T? cached))
        {
            return cached!;
        }

        T result;
        try
        {
            result = query();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Active Directory sorgusu basarisiz oldu: {Key}", cacheKey);
            throw;
        }

        cache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));
        return result;
    }

    private IReadOnlyList<T> Search<T>(string filter, string[] properties, int take, Func<ResultPropertyCollection, T?> map)
        where T : class
    {
        using var root = CreateSearchRoot();
        using var searcher = new DirectorySearcher(root)
        {
            Filter = filter,
            SizeLimit = take,
            PageSize = Math.Min(take, 500)
        };

        foreach (var property in properties)
        {
            searcher.PropertiesToLoad.Add(property);
        }

        var items = new List<T>();
        using var results = searcher.FindAll();
        foreach (SearchResult result in results)
        {
            var mapped = map(result.Properties);
            if (mapped is not null)
            {
                items.Add(mapped);
            }

            if (items.Count >= take)
            {
                break;
            }
        }

        return items;
    }

    private PrincipalContext CreateContext()
        => string.IsNullOrWhiteSpace(_options.ServiceAccountUserName)
            ? new PrincipalContext(ContextType.Domain, _options.Domain)
            : new PrincipalContext(ContextType.Domain, _options.Domain, _options.ServiceAccountUserName, _options.ServiceAccountPassword);

    private DirectoryEntry CreateSearchRoot()
    {
        var path = !string.IsNullOrWhiteSpace(_options.SearchRoot)
            ? $"LDAP://{_options.SearchRoot}"
            : !string.IsNullOrWhiteSpace(_options.Domain)
                ? $"LDAP://{_options.Domain}"
                : null;

        return string.IsNullOrWhiteSpace(_options.ServiceAccountUserName)
            ? (path is null ? new DirectoryEntry() : new DirectoryEntry(path))
            : new DirectoryEntry(path, _options.ServiceAccountUserName, _options.ServiceAccountPassword);
    }

    private static DirectoryUser? ReadUser(ResultPropertyCollection properties)
    {
        var sid = ReadSid(properties["objectSid"].Count > 0 ? properties["objectSid"][0] : null);
        var sam = ReadString(properties, "sAMAccountName");
        if (sid is null || string.IsNullOrWhiteSpace(sam))
        {
            return null;
        }

        var uac = ReadInt(properties, "userAccountControl");

        return new DirectoryUser
        {
            Sid = sid,
            SamAccountName = sam,
            UserPrincipalName = ReadString(properties, "userPrincipalName"),
            DisplayName = ReadString(properties, "displayName")
                          ?? $"{ReadString(properties, "givenName")} {ReadString(properties, "sn")}".Trim()
                          ?? sam,
            Email = ReadString(properties, "mail"),
            Title = ReadString(properties, "title"),
            Department = ReadString(properties, "department"),
            Company = ReadString(properties, "company"),
            OfficePhone = ReadString(properties, "telephoneNumber"),
            MobilePhone = ReadString(properties, "mobile"),
            ManagerDistinguishedName = ReadString(properties, "manager"),
            DistinguishedName = ReadString(properties, "distinguishedName"),
            // userAccountControl bit 2 (ACCOUNTDISABLE) set ise hesap devre disidir.
            IsActive = uac is null || (uac.Value & 0x2) == 0
        };
    }

    private static DirectoryUser? ReadUser(PropertyCollection properties)
    {
        var sid = ReadSid(properties["objectSid"].Value);
        var sam = properties["sAMAccountName"].Value as string;
        if (sid is null || string.IsNullOrWhiteSpace(sam))
        {
            return null;
        }

        return new DirectoryUser
        {
            Sid = sid,
            SamAccountName = sam,
            UserPrincipalName = properties["userPrincipalName"].Value as string,
            DisplayName = properties["displayName"].Value as string ?? sam,
            Email = properties["mail"].Value as string,
            Title = properties["title"].Value as string,
            Department = properties["department"].Value as string,
            Company = properties["company"].Value as string,
            OfficePhone = properties["telephoneNumber"].Value as string,
            MobilePhone = properties["mobile"].Value as string,
            ManagerDistinguishedName = properties["manager"].Value as string,
            DistinguishedName = properties["distinguishedName"].Value as string
        };
    }

    private static DirectoryGroup? ReadGroup(ResultPropertyCollection properties)
    {
        var sid = ReadSid(properties["objectSid"].Count > 0 ? properties["objectSid"][0] : null);
        var name = ReadString(properties, "sAMAccountName");
        if (sid is null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new DirectoryGroup
        {
            Sid = sid,
            Name = name,
            DisplayName = ReadString(properties, "displayName") ?? name,
            Description = ReadString(properties, "description"),
            Email = ReadString(properties, "mail"),
            DistinguishedName = ReadString(properties, "distinguishedName")
        };
    }

    private static string? ReadString(ResultPropertyCollection properties, string name)
        => properties[name].Count > 0 ? properties[name][0]?.ToString() : null;

    private static int? ReadInt(ResultPropertyCollection properties, string name)
        => properties[name].Count > 0 && int.TryParse(properties[name][0]?.ToString(), out var value) ? value : null;

    private static string? ReadSid(object? raw)
        => raw is byte[] bytes ? new SecurityIdentifier(bytes, 0).Value : raw?.ToString();

    /// <summary>LDAP filtrelerinde ozel anlami olan karakterleri kacisla yazar (RFC 4515).</summary>
    private static string EscapeLdapFilter(string value)
        => value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00")
            .Replace("/", "\\2f");
}
