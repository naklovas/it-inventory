using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BookRunner.Web.Services;

/// <summary>
/// REST API istemcisi. Frontend hicbir veriye dogrudan erismez; tum islemler
/// bu istemci uzerinden, kullanicinin Windows kimligiyle API'ye gider.
/// </summary>
public sealed class BookRunnerApiClient(HttpClient httpClient, ILogger<BookRunnerApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ---------------------------------------------------------------- runbook

    public Task<PagedResult<RunbookListItemDto>?> ListRunbooksAsync(RunbookFilter filter, CancellationToken ct = default)
        => GetAsync<PagedResult<RunbookListItemDto>>(BuildRunbookQuery("api/runbooks", filter), ct);

    public Task<RunbookDetailDto?> GetRunbookAsync(Guid id, CancellationToken ct = default)
        => GetAsync<RunbookDetailDto>($"api/runbooks/{id}", ct);

    public Task<DashboardDto?> GetDashboardAsync(CancellationToken ct = default)
        => GetAsync<DashboardDto>("api/runbooks/dashboard", ct);

    public Task<RunbookDetailDto?> CreateRunbookAsync(CreateRunbookRequest request, CancellationToken ct = default)
        => PostAsync<CreateRunbookRequest, RunbookDetailDto>("api/runbooks", request, ct);

    public Task<RunbookDetailDto?> UpdateRunbookAsync(Guid id, UpdateRunbookRequest request, CancellationToken ct = default)
        => PutAsync<UpdateRunbookRequest, RunbookDetailDto>($"api/runbooks/{id}", request, ct);

    public Task DeleteRunbookAsync(Guid id, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/runbooks/{id}", ct);

    public Task<RunbookDetailDto?> SaveAsTemplateAsync(Guid id, string title, string? category, CancellationToken ct = default)
        => PostAsync<object, RunbookDetailDto>($"api/runbooks/{id}/save-as-template", new { title, category }, ct);

    public Task<RunbookDetailDto?> CreateFromTemplateAsync(Guid templateId, CreateFromTemplateRequest request, CancellationToken ct = default)
        => PostAsync<CreateFromTemplateRequest, RunbookDetailDto>($"api/runbooks/templates/{templateId}/instantiate", request, ct);

    // ------------------------------------------------------------------- task

    public Task<RunbookTaskDto?> CreateTaskAsync(Guid runbookId, CreateTaskRequest request, CancellationToken ct = default)
        => PostAsync<CreateTaskRequest, RunbookTaskDto>($"api/runbooks/{runbookId}/tasks", request, ct);

    public Task<RunbookTaskDto?> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
        => PutAsync<UpdateTaskRequest, RunbookTaskDto>($"api/tasks/{taskId}", request, ct);

    public Task<RunbookTaskDto?> ChangeTaskStatusAsync(Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
        => PostAsync<ChangeTaskStatusRequest, RunbookTaskDto>($"api/tasks/{taskId}/status", request, ct);

    public Task ReorderTasksAsync(Guid runbookId, ReorderTasksRequest request, CancellationToken ct = default)
        => PostAsync<ReorderTasksRequest, object>($"api/runbooks/{runbookId}/tasks/reorder", request, ct);

    public Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/tasks/{taskId}", ct);

    public Task<IReadOnlyList<TaskActivityDto>?> GetTaskHistoryAsync(Guid taskId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<TaskActivityDto>>($"api/tasks/{taskId}/history", ct);

    // ------------------------------------------------------------- assignment

    public Task<TaskAssignmentDto?> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct = default)
        => PostAsync<AssignTaskRequest, TaskAssignmentDto>($"api/tasks/{taskId}/assignments", request, ct);

    public Task<TaskAssignmentDto?> HandoverAsync(Guid taskId, HandoverTaskRequest request, CancellationToken ct = default)
        => PostAsync<HandoverTaskRequest, TaskAssignmentDto>($"api/tasks/{taskId}/assignments/handover", request, ct);

    public Task RemoveAssignmentAsync(Guid taskId, Guid assignmentId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/tasks/{taskId}/assignments/{assignmentId}", ct);

    public Task<IReadOnlyList<TaskAssignmentDto>?> ListAssignmentsAsync(Guid taskId, bool includeInactive, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<TaskAssignmentDto>>($"api/tasks/{taskId}/assignments?includeInactive={includeInactive}", ct);

    // ---------------------------------------------------------------- comment

    public Task<TaskCommentDto?> AddCommentAsync(Guid taskId, CreateCommentRequest request, CancellationToken ct = default)
        => PostAsync<CreateCommentRequest, TaskCommentDto>($"api/tasks/{taskId}/comments", request, ct);

    public Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/comments/{commentId}", ct);

    // -------------------------------------------------------------- directory

    public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default)
        => GetAsync<CurrentUserDto>("api/directory/me", ct);

    public Task<IReadOnlyList<PersonSummary>?> SearchUsersAsync(string term, int take = 15, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<PersonSummary>>($"api/directory/users?term={Uri.EscapeDataString(term)}&take={take}", ct);

    public Task<IReadOnlyList<GroupSummary>?> SearchGroupsAsync(string term, int take = 15, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<GroupSummary>>($"api/directory/groups?term={Uri.EscapeDataString(term)}&take={take}", ct);

    public Task<IReadOnlyList<PersonSummary>?> GetGroupMembersAsync(Guid groupId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<PersonSummary>>($"api/directory/groups/{groupId}/members", ct);

    /// <summary>Kullanicinin AD fotografini API'den akis olarak getirir.</summary>
    public async Task<(byte[] Content, string ContentType)?> GetUserPhotoAsync(Guid userId, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"api/directory/users/{userId}/photo", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        return (content, response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
    }

    // ----------------------------------------------------------------- export

    public Task<FileDownload?> ExportRunbookExcelAsync(Guid runbookId, CancellationToken ct = default)
        => DownloadAsync($"api/runbooks/{runbookId}/export/excel", ct);

    public Task<FileDownload?> ExportRunbookPdfAsync(Guid runbookId, CancellationToken ct = default)
        => DownloadAsync($"api/runbooks/{runbookId}/export/pdf", ct);

    public Task<FileDownload?> ExportRunbookListExcelAsync(RunbookFilter filter, CancellationToken ct = default)
        => DownloadAsync(BuildRunbookQuery("api/runbooks/export/excel", filter), ct);

    public Task<FileDownload?> GetImportTemplateAsync(CancellationToken ct = default)
        => DownloadAsync("api/runbooks/import/template", ct);

    /// <summary>Excel dosyasini API'ye iletir ve ice aktarim sonucunu dondurur.</summary>
    public async Task<ImportResult?> ImportTasksAsync(
        Guid runbookId, Stream content, string fileName, bool commit, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fileContent, "file", fileName);

        using var response = await httpClient.PostAsync(
            $"api/runbooks/{runbookId}/import/excel?commit={commit}", form, ct);

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ImportResult>(JsonOptions, ct);
    }

    // ------------------------------------------------------------------ audit

    public Task<PagedResult<AuditLogDto>?> ListAuditAsync(AuditFilter filter, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["userName"] = filter.UserName,
            ["action"] = filter.Action?.ToString(),
            ["entityType"] = filter.EntityType,
            ["entityId"] = filter.EntityId,
            ["runbookId"] = filter.RunbookId?.ToString(),
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        };

        return GetAsync<PagedResult<AuditLogDto>>(QueryHelpers.AddQueryString("api/audit", parameters), ct);
    }

    // ---------------------------------------------------------- service manager

    public Task<IReadOnlyList<ServiceManagerWorkItem>?> SearchWorkItemsAsync(string term, int take = 15, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ServiceManagerWorkItem>>(
            $"api/service-manager/work-items?term={Uri.EscapeDataString(term)}&take={take}", ct);

    public Task<ServiceManagerHealth?> GetServiceManagerHealthAsync(CancellationToken ct = default)
        => GetAsync<ServiceManagerHealth>("api/service-manager/health", ct);

    // ---------------------------------------------------------------- scripts

    public Task<IReadOnlyList<ScriptDto>?> ListScriptsAsync(Guid? runbookId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ScriptDto>>(
            runbookId.HasValue ? $"api/scripts?runbookId={runbookId}" : "api/scripts", ct);

    public Task<ScriptRunResult?> RunScriptAsync(Guid scriptId, RunScriptRequest request, CancellationToken ct = default)
        => PostAsync<RunScriptRequest, ScriptRunResult>($"api/scripts/{scriptId}/run", request, ct);

    // --------------------------------------------------------------- yardimci

    private static string BuildRunbookQuery(string path, RunbookFilter filter)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["search"] = filter.Search,
            ["isTemplate"] = filter.IsTemplate?.ToString(),
            ["templateCategory"] = filter.TemplateCategory,
            ["ownerUserId"] = filter.OwnerUserId?.ToString(),
            ["assignedToUserId"] = filter.AssignedToUserId?.ToString(),
            ["assignedToGroupId"] = filter.AssignedToGroupId?.ToString(),
            ["tag"] = filter.Tag,
            ["serviceManagerWorkItemId"] = filter.ServiceManagerWorkItemId,
            ["plannedStartFrom"] = filter.PlannedStartFrom?.ToString("O"),
            ["plannedStartTo"] = filter.PlannedStartTo?.ToString("O"),
            ["sortBy"] = filter.SortBy,
            ["sortDescending"] = filter.SortDescending.ToString(),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        };

        var url = QueryHelpers.AddQueryString(path, parameters);

        // Dizi parametreleri QueryHelpers tarafindan desteklenmedigi icin ayrica eklenir.
        if (filter.Statuses is { Length: > 0 })
        {
            url += (url.Contains('?') ? "&" : "?") +
                   string.Join('&', filter.Statuses.Select(s => $"statuses={s}"));
        }

        return url;
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(url, body, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);

        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
    }

    private async Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
    {
        using var response = await httpClient.PutAsJsonAsync(url, body, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
    }

    private async Task SendAsync(HttpMethod method, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task<FileDownload?> DownloadAsync(string url, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            await EnsureSuccessAsync(response, ct);
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? "bookrunner-export";

        return new FileDownload(
            content,
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            fileName);
    }

    /// <summary>API'nin dondurdugu ProblemDetails icerigini anlamli bir istisnaya cevirir.</summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning("API hatasi {Status} {Url}: {Body}",
            (int)response.StatusCode, response.RequestMessage?.RequestUri, body);

        var message = TryReadProblemDetail(body) ?? $"API {(int)response.StatusCode} dondu.";
        throw new ApiException(response.StatusCode, message);
    }

    private static string? TryReadProblemDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }

            return document.RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>API'den indirilen dosya.</summary>
public sealed record FileDownload(byte[] Content, string ContentType, string FileName);

/// <summary>API'nin dondurdugu hatayi tasiyan istisna.</summary>
public sealed class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
