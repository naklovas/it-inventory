using System.ComponentModel.DataAnnotations;

namespace BookRunner.Infrastructure.Email;

/// <summary>SMTP ayarlari (appsettings: "Email").</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>false ise e-postalar kuyruga yazilir ama gonderilmez (gelistirme ortami).</summary>
    public bool Enabled { get; set; } = true;

    [Required]
    public string Host { get; set; } = "smtp.contoso.com";

    [Range(1, 65535)]
    public int Port { get; set; } = 25;

    public bool UseStartTls { get; set; }

    /// <summary>Bos birakilirsa uygulamanin Windows kimligiyle anonim baglanti denenir.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    [Required]
    public string FromAddress { get; set; } = "bookrunner@contoso.com";

    public string FromDisplayName { get; set; } = "BookRunner";

    /// <summary>Kullanicinin arayuze donebilmesi icin e-postalara eklenen taban adres.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>Bir e-posta icin en fazla deneme sayisi.</summary>
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Arka plan gondericinin kuyrugu tarama araligi (saniye).</summary>
    [Range(5, 3600)]
    public int PollingSeconds { get; set; } = 30;

    /// <summary>Bir turda gonderilecek en fazla mesaj.</summary>
    [Range(1, 200)]
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Doluysa tum e-postalar gercek alici yerine bu adrese gonderilir.
    /// Test ortamlarinda gercek kullanicilara posta gitmesini onler.
    /// </summary>
    public string? RedirectAllTo { get; set; }
}
