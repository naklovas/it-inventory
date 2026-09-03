namespace MailRelay.Service.Models;

// dbo.MailQueue.Status alaninda saklanan degerler. Enum yerine sabit string kullaniliyor
// ki veritabaninda okunabilir kalsin (raporlama/ad-hoc sorgu icin).
public static class MailStatus
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Sent = "Sent";
    public const string Retrying = "Retrying";
    public const string Failed = "Failed";
}
