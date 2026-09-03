using MailRelay.Service.Models;

namespace MailRelay.Service.PersonnelDirectory;

// TeamCatalogSyncService'in periyodik olarak yazdigi, admin panelindeki takim filtresi
// gibi okumalarin thread-safe okudugu bellek ici tutucu.
public sealed class TeamCatalogStore
{
    private volatile IReadOnlyList<TeamInfo> _teams = Array.Empty<TeamInfo>();

    public IReadOnlyList<TeamInfo> Teams => _teams;
    public DateTime? LastSyncAtUtc { get; private set; }

    public void Replace(IReadOnlyList<TeamInfo> teams)
    {
        _teams = teams;
        LastSyncAtUtc = DateTime.UtcNow;
    }
}
