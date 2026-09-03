using MailRelay.Service.Data;
using MailRelay.Service.Models;
using MailRelay.Service.PersonnelDirectory;

namespace MailRelay.Service.Services;

// POST /api/mail/send istegini kuyruga yazan ve isleme sinyali gonderen ust duzey servis.
// Kayit DB'ye yazildiktan SONRA kanal'a sinyal gonderilir; boylece kanal sinyali kaybolsa
// bile (kapasite asimi ya da restart) veri MailQueueProcessor'daki periyodik taramada yakalanir.
public sealed class MailSubmissionService
{
    private readonly MailQueueRepository _repository;
    private readonly MailQueueChannel _channel;
    private readonly IPersonnelDirectoryClient _personnelDirectory;
    private readonly Options.QueueOptions _queueOptions;

    public MailSubmissionService(
        MailQueueRepository repository,
        MailQueueChannel channel,
        IPersonnelDirectoryClient personnelDirectory,
        Microsoft.Extensions.Options.IOptions<Options.QueueOptions> queueOptions)
    {
        _repository = repository;
        _channel = channel;
        _personnelDirectory = personnelDirectory;
        _queueOptions = queueOptions.Value;
    }

    public async Task<long> SubmitAsync(MailSendRequest request, int? clientApplicationId, int? sourcePort, CancellationToken ct)
    {
        string? requestedByTeam = null;
        var requestedByUsername = string.IsNullOrWhiteSpace(request.RequestedByUsername) ? null : request.RequestedByUsername.Trim();

        if (requestedByUsername is not null)
        {
            var personnel = await _personnelDirectory.LookupAsync(requestedByUsername, ct);
            requestedByTeam = personnel?.TeamName;
        }

        var item = new MailQueueItem
        {
            ClientApplicationId = clientApplicationId,
            RequestedByUsername = requestedByUsername,
            RequestedByTeam = requestedByTeam,
            ToAddresses = string.Join(";", request.To.Select(a => a.Trim()).Where(a => a.Length > 0)),
            CcAddresses = Join(request.Cc),
            BccAddresses = Join(request.Bcc),
            Subject = request.Subject,
            Body = request.Body,
            IsBodyHtml = request.IsBodyHtml,
            Priority = Math.Clamp(request.Priority, 1, 5),
            MaxAttempts = _queueOptions.DefaultMaxAttempts,
            CorrelationId = request.CorrelationId,
            SourcePort = sourcePort,
        };

        var id = await _repository.InsertAsync(item, request.Attachments, ct);
        _channel.TryEnqueue(id);
        return id;
    }

    private static string? Join(List<string>? addresses) =>
        addresses is { Count: > 0 } ? string.Join(";", addresses.Select(a => a.Trim()).Where(a => a.Length > 0)) : null;
}
