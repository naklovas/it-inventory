using BookRunner.Application.Abstractions;
using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Infrastructure.Persistence;

/// <summary>
/// Uygulamanin EF Core baglami. Yapilandirmalar ayri
/// <c>IEntityTypeConfiguration</c> siniflarinda tutulur.
/// </summary>
public class BookRunnerDbContext(DbContextOptions<BookRunnerDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppGroup> Groups => Set<AppGroup>();
    public DbSet<AppUserGroup> UserGroups => Set<AppUserGroup>();
    public DbSet<RoleMapping> RoleMappings => Set<RoleMapping>();
    public DbSet<Runbook> Runbooks => Set<Runbook>();
    public DbSet<RunbookTask> Tasks => Set<RunbookTask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<TaskAssignment> Assignments => Set<TaskAssignment>();
    public DbSet<TaskComment> Comments => Set<TaskComment>();
    public DbSet<TaskActivity> Activities => Set<TaskActivity>();
    public DbSet<RunbookScript> Scripts => Set<RunbookScript>();
    public DbSet<ScriptExecution> ScriptExecutions => Set<ScriptExecution>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailOutboxMessage> EmailOutbox => Set<EmailOutboxMessage>();
    public DbSet<GamificationEvent> GamificationEvents => Set<GamificationEvent>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<RunbookCollaborator> RunbookCollaborators => Set<RunbookCollaborator>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bookrunner");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookRunnerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
