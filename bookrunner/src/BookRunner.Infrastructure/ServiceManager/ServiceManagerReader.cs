using System.Diagnostics;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.ServiceManager;

/// <summary>
/// Service Manager'a SDK/konsol yerine dogrudan veritabani seviyesinden,
/// salt-okunur olarak erisir. Sorgular yapilandirmadan gelir; boylece SCSM
/// surumu veya ozellestirmeleri degistiginde kod degistirmek gerekmez.
/// </summary>
public sealed class ServiceManagerReader(
    IOptions<ServiceManagerOptions> options,
    IMemoryCache cache,
    ILogger<ServiceManagerReader> logger) : IServiceManagerReader
{
    private readonly ServiceManagerOptions _options = options.Value;

    public async Task<IReadOnlyList<ServiceManagerWorkItem>> SearchWorkItemsAsync(
        string term, int take, CancellationToken ct = default)
    {
        if (!IsUsable() || string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<ServiceManagerWorkItem>();
        }

        take = Math.Clamp(take, 1, 200);
        var cacheKey = $"scsm:search:{term.Trim().ToLowerInvariant()}:{take}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<ServiceManagerWorkItem>? cached))
        {
            return cached!;
        }

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            var rows = await connection.QueryAsync<ServiceManagerRow>(new CommandDefinition(
                _options.SearchQuery,
                new { term = term.Trim(), take },
                commandTimeout: _options.CommandTimeoutSeconds,
                cancellationToken: ct));

            var items = rows.Select(Map).ToList();
            cache.Set(cacheKey, (IReadOnlyList<ServiceManagerWorkItem>)items, TimeSpan.FromMinutes(5));
            return items;
        }
        catch (Exception ex)
        {
            // SCSM erisilemedigi icin runbook calismasi durmamali.
            logger.LogError(ex, "Service Manager aramasi basarisiz oldu.");
            return Array.Empty<ServiceManagerWorkItem>();
        }
    }

    public async Task<ServiceManagerWorkItem?> GetWorkItemAsync(string id, CancellationToken ct = default)
    {
        if (!IsUsable() || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            var row = await connection.QueryFirstOrDefaultAsync<ServiceManagerRow>(new CommandDefinition(
                _options.GetByIdQuery,
                new { id = id.Trim() },
                commandTimeout: _options.CommandTimeoutSeconds,
                cancellationToken: ct));

            return row is null ? null : Map(row);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Id} numarali Service Manager kaydi okunamadi.", id);
            return null;
        }
    }

    public async Task<ServiceManagerHealth> CheckHealthAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new ServiceManagerHealth { IsEnabled = false, IsReachable = false };
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return new ServiceManagerHealth
            {
                IsEnabled = true,
                IsReachable = false,
                Error = "Baglanti dizesi tanimlanmamis."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var builder = new SqlConnectionStringBuilder(_options.ConnectionString);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(ct);
            await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT 1", commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

            stopwatch.Stop();
            return new ServiceManagerHealth
            {
                IsEnabled = true,
                IsReachable = true,
                Server = builder.DataSource,
                Database = builder.InitialCatalog,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "Service Manager veritabanina erisilemedi.");
            return new ServiceManagerHealth
            {
                IsEnabled = true,
                IsReachable = false,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private bool IsUsable()
    {
        if (!_options.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            logger.LogWarning("Service Manager etkin ama baglanti dizesi tanimlanmamis.");
            return false;
        }

        return true;
    }

    private static ServiceManagerWorkItem Map(ServiceManagerRow row) => new()
    {
        Id = row.Id ?? string.Empty,
        Title = row.Title ?? string.Empty,
        Description = row.Description,
        Status = row.Status,
        Category = row.Category,
        AssignedTo = row.AssignedTo,
        CreatedBy = row.CreatedBy,
        CreatedDate = ToOffset(row.CreatedDate),
        ScheduledStartDate = ToOffset(row.ScheduledStartDate),
        ScheduledEndDate = ToOffset(row.ScheduledEndDate),
        WorkItemType = row.WorkItemType
    };

    /// <summary>SCSM tarihleri UTC olarak saklanir.</summary>
    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;

    /// <summary>Dapper'in doldurdugu ham satir.</summary>
    private sealed class ServiceManagerRow
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? AssignedTo { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ScheduledStartDate { get; set; }
        public DateTime? ScheduledEndDate { get; set; }
        public string? WorkItemType { get; set; }
    }
}
