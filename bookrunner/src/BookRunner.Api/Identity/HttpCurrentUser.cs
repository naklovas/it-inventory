using System.Security.Claims;
using BookRunner.Application.Abstractions;
using BookRunner.Domain.Enums;

namespace BookRunner.Api.Identity;

/// <summary>
/// Istegi yapan Windows kullanicisini claim'ler uzerinden is katmanina tasir.
/// Claim'ler <see cref="Authorization.BookRunnerClaimsTransformation"/> tarafindan
/// doldurulur.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Uygulamanin kendi ekledigi claim turleri.</summary>
    public const string UserIdClaim = "bookrunner:userid";
    public const string RoleClaim = "bookrunner:role";
    /// <summary>
    /// Test modu icin: kullanicinin GERCEK rolu. Yalnizca bir yonetici kendini
    /// baska bir rol gibi goruntulerken RoleClaim'den farkli olur.
    /// </summary>
    public const string RealRoleClaim = "bookrunner:realrole";
    public const string GroupSidClaim = "bookrunner:groupsid";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string UserName => Principal?.Identity?.Name ?? "SYSTEM";

    public string DisplayName =>
        Principal?.FindFirst(ClaimTypes.GivenName)?.Value
        ?? Principal?.FindFirst("displayName")?.Value
        ?? UserName;

    public string? Sid => Principal?.FindFirst(ClaimTypes.PrimarySid)?.Value
                          ?? Principal?.FindFirst(ClaimTypes.Sid)?.Value;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(UserIdClaim)?.Value, out var id) ? id : null;

    public IReadOnlyCollection<string> GroupSids =>
        Principal?.FindAll(GroupSidClaim).Select(c => c.Value).ToArray() ?? [];

    public AppRole Role =>
        Enum.TryParse<AppRole>(Principal?.FindFirst(RoleClaim)?.Value, out var role) ? role : AppRole.Viewer;

    public AppRole RealRole =>
        Enum.TryParse<AppRole>(Principal?.FindFirst(RealRoleClaim)?.Value, out var role) ? role : Role;

    public bool IsImpersonating => Role != RealRole;

    public bool IsInRole(AppRole role) => Role >= role;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
