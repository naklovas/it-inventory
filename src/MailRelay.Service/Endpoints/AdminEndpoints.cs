using System.Security.Cryptography;
using MailRelay.Service.Data;
using MailRelay.Service.Models;
using MailRelay.Service.PersonnelDirectory;
using MailRelay.Service.Security;
using MailRelay.Service.Services;

namespace MailRelay.Service.Endpoints;

public sealed class MailLogQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Username { get; set; }
    public string? Team { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").AddEndpointFilter<AdminApiKeyFilter>();

        MapRelaySettings(group);
        MapMailLogs(group);
        MapClientApplications(group);
        MapTeams(group);
    }

    private static void MapRelaySettings(RouteGroupBuilder group)
    {
        group.MapGet("/relay-settings", async (RelaySettingsRepository repository, CancellationToken ct) =>
        {
            var settings = await repository.GetAsync(ct);
            return settings is null ? Results.NotFound() : Results.Ok(ToView(settings));
        });

        group.MapPut("/relay-settings", async (
            RelaySettingsUpdateRequest request,
            HttpContext http,
            RelaySettingsRepository repository,
            RelaySettingsCache cache,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Host))
                return Results.BadRequest(new { error = "host bos olamaz." });
            if (string.IsNullOrWhiteSpace(request.FromAddress))
                return Results.BadRequest(new { error = "fromAddress bos olamaz." });

            var updatedBy = http.Request.Headers.TryGetValue("X-Admin-User", out var u) ? u.ToString() : null;
            await repository.UpdateAsync(request, updatedBy, ct);
            cache.Invalidate();

            var settings = await repository.GetAsync(ct);
            return Results.Ok(ToView(settings!));
        });
    }

    private static void MapMailLogs(RouteGroupBuilder group)
    {
        group.MapGet("/mail-logs", async ([AsParameters] MailLogQuery query, MailQueueRepository repository, CancellationToken ct) =>
        {
            var filter = new MailLogSearchFilter
            {
                SearchText = query.Search,
                Status = query.Status,
                RequestedByUsername = query.Username,
                RequestedByTeam = query.Team,
                FromUtc = query.From,
                ToUtc = query.To,
                Page = query.Page ?? 1,
                PageSize = query.PageSize ?? 25,
            };

            var result = await repository.SearchAsync(filter, ct);
            return Results.Ok(new
            {
                result.Page,
                result.PageSize,
                result.TotalCount,
                items = result.Items.Select(i => new
                {
                    i.Id,
                    i.RequestedByUsername,
                    i.RequestedByTeam,
                    i.ToAddresses,
                    i.Subject,
                    i.Status,
                    i.Attempts,
                    i.CreatedAtUtc,
                    i.SentAtUtc,
                    i.LastError,
                }),
            });
        });

        group.MapGet("/mail-logs/{id:long}", async (long id, MailQueueRepository repository, CancellationToken ct) =>
        {
            var item = await repository.GetByIdAsync(id, ct);
            if (item is null)
                return Results.NotFound();

            var attachments = await repository.GetAttachmentsAsync(id, ct);
            return Results.Ok(new
            {
                item.Id,
                item.ClientApplicationId,
                item.RequestedByUsername,
                item.RequestedByTeam,
                item.ToAddresses,
                item.CcAddresses,
                item.BccAddresses,
                item.Subject,
                item.Body,
                item.IsBodyHtml,
                item.FromDisplayNameOverride,
                item.Status,
                item.Attempts,
                item.MaxAttempts,
                item.LastError,
                item.CorrelationId,
                item.SourcePort,
                item.CreatedAtUtc,
                item.SentAtUtc,
                item.NextAttemptAtUtc,
                attachments = attachments.Select(a => new { a.Id, a.FileName, a.ContentType, sizeBytes = a.Content.Length }),
            });
        });
    }

    private static void MapClientApplications(RouteGroupBuilder group)
    {
        group.MapGet("/client-applications", async (ClientApplicationRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.GetAllAsync(ct)));

        group.MapPost("/client-applications", async (ClientApplicationCreateRequest request, ClientApplicationRepository repository, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "name bos olamaz." });

            var apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var created = await repository.CreateAsync(request.Name.Trim(), apiKey, ct);

            // apiKey degeri sadece bu yanitta acik metin olarak doner - admin panelinde
            // bir kere gosterilip guvenli sekilde saklanmasi gerektigi belirtilir.
            return Results.Ok(created);
        });

        group.MapPut("/client-applications/{id:int}/enabled", async (int id, ClientApplicationEnabledRequest request, ClientApplicationRepository repository, CancellationToken ct) =>
        {
            var updated = await repository.SetEnabledAsync(id, request.Enabled, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        });
    }

    private static void MapTeams(RouteGroupBuilder group)
    {
        group.MapGet("/teams", (TeamCatalogStore store) => Results.Ok(new
        {
            lastSyncAtUtc = store.LastSyncAtUtc,
            teams = store.Teams.Select(t => t.EkipAdi).OrderBy(n => n),
        }));
    }

    private static RelaySettingsView ToView(RelaySettings s) => new()
    {
        Enabled = s.Enabled,
        Host = s.Host,
        Port = s.Port,
        EnableSsl = s.EnableSsl,
        Username = s.Username,
        HasPassword = !string.IsNullOrEmpty(s.Password),
        FromAddress = s.FromAddress,
        FromDisplayName = s.FromDisplayName,
        MaxConcurrentSend = s.MaxConcurrentSend,
        UpdatedAtUtc = s.UpdatedAtUtc,
        UpdatedBy = s.UpdatedBy,
    };
}

public sealed class ClientApplicationEnabledRequest
{
    public bool Enabled { get; set; }
}
