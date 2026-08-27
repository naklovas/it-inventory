using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Email;

/// <summary>
/// E-postalari dogrudan gondermek yerine outbox tablosuna yazar. Gercek gonderimi
/// <see cref="EmailOutboxProcessor"/> yapar; boylece SMTP arizasi kullanici islemini
/// bozmaz ve hangi bildirimin kime gittigi denetlenebilir kalir.
/// </summary>
public sealed class OutboxEmailSender(
    BookRunnerDbContext db,
    IOptions<EmailOptions> options,
    ILogger<OutboxEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var recipients = Normalize(message.To);
        if (recipients.Count == 0)
        {
            logger.LogDebug("Alicisi olmayan bildirim atlandi: {Reason}", message.Reason);
            return;
        }

        var cc = Normalize(message.Cc);

        if (!string.IsNullOrWhiteSpace(_options.RedirectAllTo))
        {
            // Test ortami: gercek alicilar konu satirinda gorunur, posta tek adrese gider.
            recipients = [_options.RedirectAllTo!];
            cc = [];
        }

        db.EmailOutbox.Add(new EmailOutboxMessage
        {
            To = string.Join(';', recipients),
            Cc = cc.Count == 0 ? null : string.Join(';', cc),
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            Status = EmailStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow,
            Reason = message.Reason,
            RunbookId = message.RunbookId,
            TaskId = message.TaskId
        });

        await db.SaveChangesAsync(ct);
    }

    private static List<string> Normalize(IReadOnlyList<string> addresses)
        => addresses
            .Where(a => !string.IsNullOrWhiteSpace(a) && a.Contains('@'))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
