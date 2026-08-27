using BookRunner.Application.Dtos;

namespace BookRunner.Application.Abstractions;

/// <summary>
/// Active Directory okuma islemleri. Uygulama AD'ye yazmaz; kullanici, grup,
/// uyelik ve foto bilgileri yalnizca okunur.
/// </summary>
public interface IDirectoryService
{
    /// <summary>Ad, soyad, oturum adi veya e-postaya gore kullanici arar.</summary>
    Task<IReadOnlyList<DirectoryUser>> SearchUsersAsync(string term, int take, CancellationToken ct = default);

    /// <summary>Grup adina gore arama yapar.</summary>
    Task<IReadOnlyList<DirectoryGroup>> SearchGroupsAsync(string term, int take, CancellationToken ct = default);

    Task<DirectoryUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken ct = default);

    Task<DirectoryUser?> FindUserBySidAsync(string sid, CancellationToken ct = default);

    Task<DirectoryGroup?> FindGroupBySidAsync(string sid, CancellationToken ct = default);

    /// <summary>Kullanicinin (ic ice gruplar dahil) uyesi oldugu gruplarin SID listesi.</summary>
    Task<IReadOnlyList<string>> GetUserGroupSidsAsync(string samAccountName, CancellationToken ct = default);

    /// <summary>Grubun dogrudan uyelerini dondurur; goreve atanan gruptaki kisilere e-posta gonderirken kullanilir.</summary>
    Task<IReadOnlyList<DirectoryUser>> GetGroupMembersAsync(string groupSid, CancellationToken ct = default);

    /// <summary>AD'deki thumbnailPhoto/jpegPhoto icerigi. Yoksa null.</summary>
    Task<byte[]?> GetUserPhotoAsync(string sid, CancellationToken ct = default);
}
