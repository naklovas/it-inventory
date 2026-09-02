using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Services;

/// <summary>
/// Takim adi -> rol eslemelerini yonetir. Bir takima rol atamak o takimdeki
/// HERKESE o rolun tum yetkilerini vermek demektir; bu yuzden yalnizca
/// admin.manage yetkisi olanlar (Yonetici rolu) erisebilir.
/// </summary>
public sealed class RoleMappingService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : IRoleMappingService
{
    public async Task<IReadOnlyList<RoleMappingDto>> ListAsync(CancellationToken ct = default)
    {
        EnsureAdmin();

        var mappings = await db.RoleMappings.AsNoTracking()
            .OrderBy(m => m.TeamName)
            .ToListAsync(ct);

        return mappings.Select(m => m.ToDto()).ToList();
    }

    public async Task<RoleMappingDto> CreateAsync(SaveRoleMappingRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var teamName = request.TeamName.Trim();
        if (teamName.Length == 0)
        {
            throw new BusinessRuleException("Takim adi bos olamaz.");
        }

        if (await db.RoleMappings.AnyAsync(m => m.TeamName == teamName && m.Role == request.Role, ct))
        {
            throw new BusinessRuleException($"'{teamName}' takimi icin '{DisplayText.Role(request.Role)}' eslemesi zaten var.");
        }

        var mapping = new RoleMapping { TeamName = teamName, Role = request.Role, IsActive = true };
        db.RoleMappings.Add(mapping);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Create, nameof(RoleMapping), mapping.Id.ToString(),
            $"'{teamName}' takimi '{DisplayText.Role(request.Role)}' rolune eslendi.", ct: ct);

        return mapping.ToDto();
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        EnsureAdmin();

        var mapping = await db.RoleMappings.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException("Rol eslemesi", id);

        mapping.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Update, nameof(RoleMapping), id.ToString(),
            $"'{mapping.TeamName}' eslemesi {(isActive ? "etkinlestirildi" : "devre disi birakildi")}.", ct: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();

        var mapping = await db.RoleMappings.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException("Rol eslemesi", id);

        db.RoleMappings.Remove(mapping);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Delete, nameof(RoleMapping), id.ToString(),
            $"'{mapping.TeamName}' -> '{DisplayText.Role(mapping.Role)}' eslemesi silindi.", ct: ct);
    }

    private void EnsureAdmin()
    {
        if (!Permissions.Has(currentUser.Role, Permissions.AdminManage))
        {
            throw new ForbiddenException("Rol eslemelerini yonetme yetkiniz yok.");
        }
    }
}
