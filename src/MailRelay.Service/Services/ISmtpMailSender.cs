using MailRelay.Service.Models;

namespace MailRelay.Service.Services;

public interface ISmtpMailSender
{
    Task SendAsync(RelaySettings settings, MailQueueItem item, IReadOnlyList<MailAttachmentRecord> attachments, CancellationToken ct);
}
