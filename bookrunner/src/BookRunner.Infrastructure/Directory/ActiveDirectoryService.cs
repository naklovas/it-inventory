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
/// Yapilandirmada birden fazla etki alani tanimliysa sorgular hepsinde calisir.
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
            () => SearchAllDomains(filter, UserProperties, Math.Min(take, _options.MaxSearchResults), ReadUser)));
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
            () => SearchAllDomains(filter, GroupProperties, Math.Min(take, _options.MaxSearchResults), ReadGroup)));
    }

    public Task<DirectoryUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(samAccountName))
        {
            return Task.FromResult<DirectoryUser?>(null);
        }

        var (netBiosHint, accountName) = SplitAccountName(samAccountName);
        var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdapFilter(accountName)}))";

        return Task.FromResult(CacheOrQuery(
            $"ad:user:sam:{samAccountName.ToLowerInvariant()}",
            () => SearchUntilFound(filter, UserProperties, ReadUser, netBiosHint)));
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
            () => SearchUntilFound(filter, UserProperties, ReadUser)));
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
            () => SearchUntilFound(filter, GroupProperties, ReadGroup)));
    }

    public Task<IReadOnlyList<string>> GetUserGroupSidsAsync(string samAccountName, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(samAccountName))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var (netBiosHint, accountName) = SplitAccountName(samAccountName);

        return Task.FromResult(CacheOrQuery($"ad:groupsof:{samAccountName.ToLowerInvariant()}", () =>
        {
            foreach (var domain in OrderDomains(netBiosHint))
            {
                using var context = CreateContext(domain);
                using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, accountName);
                if (user is null)
                {
                    continue;
                }

                return ReadAuthorizationGroups(user, accountName);
            }

            return (IReadOnlyList<string>)Array.Empty<string>();
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
            foreach (var domain in _options.ResolveDomains())
            {
                using var context = CreateContext(domain);
                using var group = GroupPrincipal.FindByIdentity(context, IdentityType.Sid, groupSid);
                if (group is null)
                {
                    continue;
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

                return (IReadOnlyList<DirectoryUser>)members;
            }

            return Array.Empty<DirectoryUser>();
        }));
    }

    public Task<byte[]?> GetUserPhotoAsync(string sid, CancellationToken ct = default)
    {
        if (_options.Disabled || string.IsNullOrWhiteSpace(sid))
        {
            return Task.FromResult<byte[]?>(null);
        }

        foreach (var domain in _options.ResolveDomains())
        {
            try
            {
                using var root = CreateSearchRoot(domain);
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
                logger.LogWarning(ex, "{Sid} icin AD fotografi {Domain} etki alanindan okunamadi.", sid, domain.Name);
            }
        }

        return Task.FromResult<byte[]?>(null);
    }

    /// <summary>Sorguyu onbellekten karsilar.</summary>
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

    /// <summary>Tum etki alanlarinda arar ve sonuclari birlestirir.</summary>
    private IReadOnlyList<T> SearchAllDomains<T>(
        string filter, string[] properties, int take, Func<ResultPropertyCollection, T?> map)
        where T : class
    {
        var items = new List<T>();

        foreach (var domain in _options.ResolveDomains())
        {
            if (items.Count >= take)
            {
                break;
            }

            try
            {
                items.AddRange(SearchDomain(domain, filter, properties, take - items.Count, map));
            }
            catch (Exception ex)
            {
                // Bir etki alanina erisilemezse digerlerinin sonucu yine de donmelidir.
                logger.LogWarning(ex, "{Domain} etki alaninda arama basarisiz oldu; atlaniyor.", domain.Name);
            }
        }

        return items;
    }

    /// <summary>Etki alanlarini sirayla dener, ilk eslesmede durur.</summary>
    private T? SearchUntilFound<T>(
        string filter, string[] properties, Func<ResultPropertyCollection, T?> map, string? netBiosHint = null)
        where T : class
    {
        foreach (var domain in OrderDomains(netBiosHint))
        {
            try
            {
                var match = SearchDomain(domain, filter, properties, 1, map).FirstOrDefault();
                if (match is not null)
                {
                    return match;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{Domain} etki alaninda sorgu basarisiz oldu; atlaniyor.", domain.Name);
            }
        }

        return null;
    }

    private IReadOnlyList<T> SearchDomain<T>(
        DirectoryDomainOptions domain, string filter, string[] properties, int take,
        Func<ResultPropertyCollection, T?> map)
        where T : class
    {
        using var root = CreateSearchRoot(domain);
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

    /// <summary>
    /// Kullanicinin oturum actigi etki alani biliniyorsa o etki alanini basa alir;
    /// boylece cok domainli ormanlarda gereksiz sorgu yapilmaz.
    /// </summary>
    private IEnumerable<DirectoryDomainOptions> OrderDomains(string? netBiosHint)
    {
        var domains = _options.ResolveDomains();

        if (string.IsNullOrWhiteSpace(netBiosHint) || domains.Count <= 1)
        {
            return domains;
        }

        return domains
            .OrderByDescending(d => Matches(d, netBiosHint))
            .ToList();

        static bool Matches(DirectoryDomainOptions domain, string hint)
            => string.Equals(domain.NetBiosName, hint, StringComparison.OrdinalIgnoreCase)
               || (domain.Name is not null &&
                   domain.Name.StartsWith(hint + ".", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ic ice grup uyeliklerini cozer. Bir uyelik cozulemezse (orn. guven iliskisi
    /// kopuksa) o kayit atlanir, islem devam eder.
    /// </summary>
    private IReadOnlyList<string> ReadAuthorizationGroups(UserPrincipal user, string accountName)
    {
        var sids = new List<string>();

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
                logger.LogDebug(ex, "{User} icin bir grup uyeligi cozulemedi, atlaniyor.", accountName);
            }
        }

        return sids;
    }

    private static PrincipalContext CreateContext(DirectoryDomainOptions domain)
        => string.IsNullOrWhiteSpace(domain.ServiceAccountUserName)
            ? new PrincipalContext(ContextType.Domain, domain.Name)
            : new PrincipalContext(ContextType.Domain, domain.Name,
                domain.ServiceAccountUserName, domain.ServiceAccountPassword);

    private static DirectoryEntry CreateSearchRoot(DirectoryDomainOptions domain)
    {
        var path = !string.IsNullOrWhiteSpace(domain.SearchRoot)
            ? $"LDAP://{domain.SearchRoot}"
            : !string.IsNullOrWhiteSpace(domain.Name)
                ? $"LDAP://{domain.Name}"
                : null;

        return string.IsNullOrWhiteSpace(domain.ServiceAccountUserName)
            ? (path is null ? new DirectoryEntry() : new DirectoryEntry(path))
            : new DirectoryEntry(path, domain.ServiceAccountUserName, domain.ServiceAccountPassword);
    }

    /// <summary>"CONTOSO\ali" -> ("CONTOSO", "ali"); "ali@contoso.com" -> (null, "ali").</summary>
    private static (string? NetBios, string AccountName) SplitAccountName(string value)
    {
        var name = value.Trim();

        var slash = name.LastIndexOf('\\');
        if (slash >= 0)
        {
            return (name[..slash], name[(slash + 1)..]);
        }

        var at = name.IndexOf('@');
        return (null, at > 0 ? name[..at] : name);
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
