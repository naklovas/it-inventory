namespace BookRunner.Application.Dtos;

/// <summary>AD'den okunan kullanici.</summary>
public sealed record DirectoryUser
{
    public required string Sid { get; init; }
    public required string SamAccountName { get; init; }
    public string? UserPrincipalName { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Title { get; init; }
    public string? Department { get; init; }
    public string? Company { get; init; }
    public string? OfficePhone { get; init; }
    public string? MobilePhone { get; init; }
    public string? ManagerDistinguishedName { get; init; }
    public string? DistinguishedName { get; init; }
    public byte[]? Photo { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>AD'den okunan grup.</summary>
public sealed record DirectoryGroup
{
    public required string Sid { get; init; }
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Email { get; init; }
    public string? DistinguishedName { get; init; }
}

/// <summary>Arayuzde avatar/rozet gosterimi icin ozetlenmis kisi bilgisi.</summary>
public sealed record PersonSummary
{
    public Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Title { get; init; }
    public string? Department { get; init; }
    public required string Initials { get; init; }
    public required string AvatarColor { get; init; }
    public bool HasPhoto { get; init; }
    /// <summary>Fotoyu getiren API adresi; foto yoksa null.</summary>
    public string? PhotoUrl { get; init; }
}

/// <summary>Grup rozeti.</summary>
public sealed record GroupSummary
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public required string Initials { get; init; }
    public required string AvatarColor { get; init; }
    public int MemberCount { get; init; }
}

/// <summary>Oturum acan kullanicinin profili ve yetkileri.</summary>
public sealed record CurrentUserDto
{
    public Guid? Id { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Title { get; init; }
    public string? Department { get; init; }
    public required string Initials { get; init; }
    public required string AvatarColor { get; init; }
    public bool HasPhoto { get; init; }
    /// <summary>Fotografi getiren API adresi; foto yoksa null.</summary>
    public string? PhotoUrl { get; init; }
    public required string Role { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
    /// <summary>Kullanicinin GERCEK rolu Yonetici mi (test modu aktifken bile degismez).</summary>
    public bool IsAdministrator { get; init; }
    /// <summary>Su an "test modu" ile baska bir rol gibi goruntuluyor mu.</summary>
    public bool IsRoleOverridden { get; init; }
}
