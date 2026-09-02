using BookRunner.Application.Abstractions;
using BookRunner.Domain.Enums;

namespace BookRunner.Infrastructure.Identity;

/// <summary>
/// HTTP baglami olmayan calisma ortamlari (arka plan servisleri, migration,
/// tasarim zamani) icin kullanicisiz kimlik.
/// </summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public string UserName => "SYSTEM";

    public string DisplayName => "Sistem";

    public string? Sid => null;

    public Guid? UserId => null;

    public IReadOnlyCollection<string> GroupSids => Array.Empty<string>();

    public AppRole Role => AppRole.Administrator;

    public AppRole RealRole => AppRole.Administrator;

    public bool IsInRole(AppRole role) => true;

    public string? IpAddress => null;
}
