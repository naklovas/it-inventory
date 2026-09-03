namespace MailRelay.Service.Options;

// appsettings.json > PersonnelDirectory
public sealed class PersonnelDirectoryOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "";

    // {0} yerine kullanici adi konur, orn: "/api/personel/{0}"
    public string LookupPathTemplate { get; set; } = "/api/personel/{0}";
    public string TeamsPath { get; set; } = "/api/takimlar";
    public int TeamCatalogSyncMinutes { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 5;
}
