namespace MailRelay.Service.Models;

// POST /api/mail/send govde modeli.
public sealed class MailAttachmentInput
{
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }

    // Base64 kodlanmis dosya icerigi.
    public string ContentBase64 { get; set; } = "";
}

public sealed class MailSendRequest
{
    public List<string> To { get; set; } = new();
    public List<string>? Cc { get; set; }
    public List<string>? Bcc { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsBodyHtml { get; set; } = true;

    // Kullanici adi verilirse PersonnelDirectory servisinden takim bilgisi ile zenginlestirilir
    // ve MailQueue.RequestedByUsername/RequestedByTeam alanlarina yazilir (raporlamada kullanilir).
    public string? RequestedByUsername { get; set; }

    // 1 (yuksek) - 5 (dusuk). Varsayilan 3.
    public int Priority { get; set; } = 3;

    // Cagiran uygulamanin kendi takip numarasi; loglarda/aramalarda kullanilir.
    public string? CorrelationId { get; set; }

    public List<MailAttachmentInput>? Attachments { get; set; }
}
