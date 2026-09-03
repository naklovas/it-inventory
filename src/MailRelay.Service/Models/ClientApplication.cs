namespace MailRelay.Service.Models;

// dbo.ClientApplications satirinin karsiligi - servise mail gonderme istegi yapabilen
// her uygulama icin bir API anahtari.
public sealed class ClientApplication
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool Enabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ClientApplicationCreateRequest
{
    public string Name { get; set; } = "";
}
