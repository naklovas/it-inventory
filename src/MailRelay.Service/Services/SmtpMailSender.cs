using System.Net;
using System.Net.Mail;
using MailRelay.Service.Models;

namespace MailRelay.Service.Services;

// dbo.RelaySettings icindeki relay hesabi uzerinden gercek SMTP gonderimini yapar.
// Adres listeleri MailQueue'da ';' ya da ',' ile ayrilmis metin olarak saklanir.
public sealed class SmtpMailSender : ISmtpMailSender
{
    public async Task SendAsync(RelaySettings settings, MailQueueItem item, IReadOnlyList<MailAttachmentRecord> attachments, CancellationToken ct)
    {
        // Gonderen e-posta ADRESI her zaman tek relay hesabindan gelir; sadece GORUNEN AD
        // (istenirse) istemcinin bu mail icin gonderdigi FromDisplayNameOverride ile degistirilebilir.
        var fromDisplayName = string.IsNullOrWhiteSpace(item.FromDisplayNameOverride) ? settings.FromDisplayName : item.FromDisplayNameOverride;

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, fromDisplayName),
            Subject = item.Subject,
            Body = item.Body,
            IsBodyHtml = item.IsBodyHtml,
        };

        foreach (var address in SplitAddresses(item.ToAddresses))
            message.To.Add(address);
        foreach (var address in SplitAddresses(item.CcAddresses))
            message.CC.Add(address);
        foreach (var address in SplitAddresses(item.BccAddresses))
            message.Bcc.Add(address);

        if (message.To.Count == 0)
            throw new InvalidOperationException("Gecerli bir alici adresi bulunamadi.");

        foreach (var attachment in attachments)
        {
            var stream = new MemoryStream(attachment.Content);
            message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType ?? "application/octet-stream"));
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000,
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var (domain, user) = SplitDomainUser(settings.Username);
            client.Credentials = string.IsNullOrEmpty(domain)
                ? new NetworkCredential(user, settings.Password ?? "")
                : new NetworkCredential(user, settings.Password ?? "", domain);
        }
        else
        {
            client.UseDefaultCredentials = true;
        }

        await client.SendMailAsync(message, ct);
    }

    private static readonly char[] AddressSeparators = [';', ','];

    private static IEnumerable<string> SplitAddresses(string? raw) =>
        (raw ?? "")
            .Split(AddressSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // "xxx\xxxx" bicimindeki Windows alan adi kullanici adini boler; ters slash yoksa
    // domain null doner ve NetworkCredential yalnizca kullanici adi/parola ile kurulur.
    private static (string? Domain, string User) SplitDomainUser(string username)
    {
        var index = username.IndexOf('\\');
        return index > 0 ? (username[..index], username[(index + 1)..]) : (null, username);
    }
}
