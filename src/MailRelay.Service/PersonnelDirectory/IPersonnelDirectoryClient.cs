using MailRelay.Service.Models;

namespace MailRelay.Service.PersonnelDirectory;

public interface IPersonnelDirectoryClient
{
    // GET {BaseUrl}{LookupPathTemplate} - kullanici bulunamazsa ya da servis
    // erisilemezse null doner (mail gonderimini bloke etmez).
    Task<PersonnelInfo?> LookupAsync(string username, CancellationToken ct);

    // GET {BaseUrl}{TeamsPath} - TeamCatalogSyncService tarafindan periyodik olarak cagirilir.
    Task<IReadOnlyList<TeamInfo>> FetchTeamsAsync(CancellationToken ct);
}
