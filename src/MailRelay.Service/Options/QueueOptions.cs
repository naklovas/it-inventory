namespace MailRelay.Service.Options;

// appsettings.json > Queue
public sealed class QueueOptions
{
    // Kuyruktan paralel tuketen worker (Task) sayisi.
    public int WorkerCount { get; set; } = 4;

    // Ayni anda relay hesabina acilabilecek en fazla SMTP baglantisi. RelaySettings'teki
    // MaxConcurrentSend degeri veritabanindan okunabiliyorsa ona; okunamazsa buna dusulur.
    public int MaxConcurrentSend { get; set; } = 4;

    // Worker'lar bos kaldiginda veritabanini yeniden Queued/Retriable kayit icin
    // taramasi arasindaki sure (yeniden baslatma sonrasi kurtarma ve retry zamanlamasi icin).
    public int PollIntervalSeconds { get; set; } = 2;

    // Bir taramada en fazla ele alinacak kayit sayisi.
    public int PollBatchSize { get; set; } = 50;

    // In-memory sinyal kanalinin kapasitesi; asilirsa yazma bloklanmaz, DB poll yakalar.
    public int ChannelCapacity { get; set; } = 10000;

    // Uslu geri cekilme (exponential backoff) taban ve tavan degerleri (saniye).
    public int BaseRetryDelaySeconds { get; set; } = 30;
    public int MaxRetryDelaySeconds { get; set; } = 1800;

    public int DefaultMaxAttempts { get; set; } = 5;

    // Admin panelinden RelaySettings guncellendiginde bellek onbellegin gecerliligi (saniye).
    public int RelaySettingsCacheSeconds { get; set; } = 30;
}
