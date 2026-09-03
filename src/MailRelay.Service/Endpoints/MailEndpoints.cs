using MailRelay.Service.Data;
using MailRelay.Service.Models;
using MailRelay.Service.Security;
using MailRelay.Service.Services;

namespace MailRelay.Service.Endpoints;

public static class MailEndpoints
{
    public static void MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mail").AddEndpointFilter<ClientApiKeyFilter>();

        group.MapPost("/send", async (MailSendRequest request, HttpContext http, MailSubmissionService submission, CancellationToken ct) =>
        {
            if (request.To is not { Count: > 0 })
                return Results.BadRequest(new { error = "En az bir alici (to) adresi gerekli." });
            if (string.IsNullOrWhiteSpace(request.Subject))
                return Results.BadRequest(new { error = "subject bos olamaz." });
            if (string.IsNullOrWhiteSpace(request.Body))
                return Results.BadRequest(new { error = "body bos olamaz." });

            var clientApp = (ClientApplication)http.Items[ClientApiKeyFilter.HttpContextItemKey]!;
            var sourcePort = http.Connection.LocalPort;

            var id = await submission.SubmitAsync(request, clientApp.Id, sourcePort, ct);
            return Results.Json(new { id, status = MailStatus.Queued }, statusCode: StatusCodes.Status202Accepted);
        });

        group.MapGet("/{id:long}/status", async (long id, HttpContext http, MailQueueRepository repository, CancellationToken ct) =>
        {
            var item = await repository.GetByIdAsync(id, ct);
            if (item is null)
                return Results.NotFound();

            var clientApp = (ClientApplication)http.Items[ClientApiKeyFilter.HttpContextItemKey]!;
            if (item.ClientApplicationId != clientApp.Id)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(new
            {
                item.Id,
                item.Status,
                item.Attempts,
                item.MaxAttempts,
                item.LastError,
                item.CreatedAtUtc,
                item.SentAtUtc,
                item.NextAttemptAtUtc,
            });
        });
    }
}
