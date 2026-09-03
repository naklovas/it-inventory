namespace MailRelay.Service.Models;

// dbo.RelaySettings satirinin karsiligi (tek satir, Id=1).
public sealed class RelaySettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string? FromDisplayName { get; set; }
    public int MaxConcurrentSend { get; set; } = 4;
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

// Admin panelinde parolayi disari sizdirmayan gorunum modeli.
public sealed class RelaySettingsView
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }
    public string FromAddress { get; set; } = "";
    public string? FromDisplayName { get; set; }
    public int MaxConcurrentSend { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class RelaySettingsUpdateRequest
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? Username { get; set; }

    // Bos/null birakilirsa mevcut parola korunur (admin panelinde her seferinde yeniden girilmez).
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string? FromDisplayName { get; set; }
    public int MaxConcurrentSend { get; set; } = 4;
}
