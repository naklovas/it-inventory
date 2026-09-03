namespace MailRelay.Service.Options;

// appsettings.json > SmtpSettings. Sadece ilk kurulumda dbo.RelaySettings tablosuna
// tohum (seed) veri olarak yazmak icin kullanilir - calisma zamaninda gercek ayarlar
// her zaman veritabanindan (admin panelinden yonetilen) okunur.
public sealed class SmtpOptions
{
    public bool Enabled { get; set; }
    public bool EnableSsl { get; set; }
    public string Host { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int Port { get; set; } = 25;
    public string FromAddress { get; set; } = "";
    public string FromDisplayName { get; set; } = "";
    public string ToAddresses { get; set; } = "";
}
