using System.Text.Json;
using BookRunner.Application.Abstractions;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Persistence;

namespace BookRunner.Infrastructure.Audit;

/// <summary>
/// Varlik degisikligi disindaki islemleri (disa aktarim, script calistirma,
/// yetkisiz erisim denemesi vb.) audit trail'e yazar.
/// </summary>
public sealed class AuditService(BookRunnerDbContext db, ICurrentUser currentUser) : IAuditService
{
    public async Task LogAsync(
        AuditAction action,
        string entityType,
        string? entityId,
        string summary,
        Guid? runbookId = null,
        object? changes = null,
        CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserName = currentUser.UserName,
            UserDisplayName = currentUser.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            RunbookId = runbookId,
            Summary = summary.Length > 1000 ? summary[..1000] : summary,
            Changes = changes is null ? null : JsonSerializer.Serialize(changes),
            IpAddress = currentUser.IpAddress
        });

        await db.SaveChangesAsync(ct);
    }
}
