using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Services;

/// <summary>Audit trail sorgulama. Yalnizca audit okuma yetkisi olanlar erisir.</summary>
public sealed class AuditQueryService(IAppDbContext db, ICurrentUser currentUser) : IAuditQueryService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditFilter filter, CancellationToken ct = default)
    {
        if (!Permissions.Has(currentUser.Role, Permissions.AuditRead))
        {
            throw new ForbiddenException("Audit kayitlarini goruntuleme yetkiniz yok.");
        }

        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            var term = $"%{filter.UserName.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.UserName, term) ||
                                     (a.UserDisplayName != null && EF.Functions.Like(a.UserDisplayName, term)));
        }

        if (filter.Action.HasValue)
        {
            query = query.Where(a => a.Action == filter.Action.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(a => a.EntityId == filter.EntityId);
        }

        if (filter.RunbookId.HasValue)
        {
            query = query.Where(a => a.RunbookId == filter.RunbookId.Value);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.To.Value);
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<AuditLogDto>.Create(logs.Select(l => l.ToDto()).ToList(), page, pageSize, total);
    }
}
