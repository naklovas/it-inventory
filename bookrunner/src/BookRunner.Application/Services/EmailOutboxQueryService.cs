using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Services;

/// <summary>Giden e-posta kuyrugunu okur. Yalnizca audit okuma yetkisi olanlar erisir.</summary>
public sealed class EmailOutboxQueryService(IAppDbContext db, ICurrentUser currentUser) : IEmailOutboxQueryService
{
    public async Task<PagedResult<EmailOutboxDto>> ListAsync(EmailOutboxFilter filter, CancellationToken ct = default)
    {
        if (!Permissions.Has(currentUser.Role, Permissions.AuditRead))
        {
            throw new ForbiddenException("E-posta kayitlarini goruntuleme yetkiniz yok.");
        }

        var query = db.EmailOutbox.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(m => m.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Reason))
        {
            query = query.Where(m => m.Reason == filter.Reason);
        }

        if (!string.IsNullOrWhiteSpace(filter.To))
        {
            var term = $"%{filter.To.Trim()}%";
            query = query.Where(m => EF.Functions.Like(m.To, term));
        }

        if (filter.RunbookId.HasValue)
        {
            query = query.Where(m => m.RunbookId == filter.RunbookId.Value);
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<EmailOutboxDto>.Create(messages.Select(m => m.ToDto()).ToList(), page, pageSize, total);
    }
}
