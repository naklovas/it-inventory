using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Abstractions;

/// <summary>
/// Is katmaninin veri erisimi icin gordugu sozlesme. Somut EF Core DbContext'i
/// Infrastructure katmanindadir; boylece is kurallari saglayicidan bagimsiz kalir.
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<AppGroup> Groups { get; }
    DbSet<AppUserGroup> UserGroups { get; }
    DbSet<RoleMapping> RoleMappings { get; }
    DbSet<Runbook> Runbooks { get; }
    DbSet<RunbookTask> Tasks { get; }
    DbSet<TaskDependency> TaskDependencies { get; }
    DbSet<TaskAssignment> Assignments { get; }
    DbSet<TaskComment> Comments { get; }
    DbSet<TaskActivity> Activities { get; }
    DbSet<RunbookScript> Scripts { get; }
    DbSet<ScriptExecution> ScriptExecutions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<EmailOutboxMessage> EmailOutbox { get; }
    DbSet<GamificationEvent> GamificationEvents { get; }
    DbSet<Badge> Badges { get; }
    DbSet<UserBadge> UserBadges { get; }
    DbSet<RunbookCollaborator> RunbookCollaborators { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
