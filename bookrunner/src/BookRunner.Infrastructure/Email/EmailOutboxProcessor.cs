using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Persistence;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BookRunner.Infrastructure.Email;

/// <summary>
/// Outbox tablosundaki bekleyen e-postalari periyodik olarak gonderir.
/// Basarisiz denemeler ustel bekleme ile tekrarlanir.
/// </summary>
public sealed class EmailOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    ILogger<EmailOutboxProcessor> logger) : BackgroundService
{
    private readonly EmailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("E-posta gonderimi kapali; outbox yalnizca kayit tutacak.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollingSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Tek bir tur basarisiz olsa da servis calismaya devam etmeli.
                logger.LogError(ex, "E-posta kuyrugu islenirken beklenmeyen hata olustu.");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookRunnerDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pending = await db.EmailOutbox
            .Where(m => m.Status == EmailStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.Id)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                ct);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, ct);
            }

            foreach (var message in pending)
            {
                await SendOneAsync(client, message, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP sunucusuna baglanilamadi ({Host}:{Port}).", _options.Host, _options.Port);
            foreach (var message in pending)
            {
                RecordFailure(message, ex.Message);
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, ct);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SendOneAsync(SmtpClient client, Domain.Entities.EmailOutboxMessage message, CancellationToken ct)
    {
        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromAddress));

            foreach (var address in message.To.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                mime.To.Add(MailboxAddress.Parse(address));
            }

            if (!string.IsNullOrWhiteSpace(message.Cc))
            {
                foreach (var address in message.Cc.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    mime.Cc.Add(MailboxAddress.Parse(address));
                }
            }

            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

            await client.SendAsync(mime, ct);

            message.Status = EmailStatus.Sent;
            message.SentAt = DateTimeOffset.UtcNow;
            message.AttemptCount++;
            message.LastError = null;

            logger.LogInformation("Bildirim gonderildi: {Subject} -> {To}", message.Subject, message.To);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bildirim gonderilemedi: {Subject} -> {To}", message.Subject, message.To);
            RecordFailure(message, ex.Message);
        }
    }

    /// <summary>Basarisiz denemeyi kaydeder ve bir sonraki denemeyi ustel olarak erteler.</summary>
    private void RecordFailure(Domain.Entities.EmailOutboxMessage message, string error)
    {
        message.AttemptCount++;
        message.LastError = error.Length > 2000 ? error[..2000] : error;

        if (message.AttemptCount >= _options.MaxAttempts)
        {
            message.Status = EmailStatus.Failed;
            message.NextAttemptAt = null;
            return;
        }

        var delayMinutes = Math.Pow(2, message.AttemptCount);
        message.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(delayMinutes);
    }
}
