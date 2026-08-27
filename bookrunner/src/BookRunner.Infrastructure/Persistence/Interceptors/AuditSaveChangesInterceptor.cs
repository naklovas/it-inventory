using System.Text.Json;
using BookRunner.Application.Abstractions;
using BookRunner.Domain.Common;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookRunner.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Kaydetme sirasinda iki is yapar: <see cref="AuditableEntity"/> alanlarini doldurur
/// ve izlenen varliklar icin degismez audit kayitlari uretir. Boylece "kim neyi ne
/// zaman degistirdi" bilgisi servislerde tek tek yazilmadan garanti altina alinir.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    /// <summary>Audit'e yazilmayacak turler (audit'in kendisi ve yuksek hacimli kayitlar).</summary>
    private static readonly HashSet<string> ExcludedTypes =
    [
        nameof(AuditLog),
        nameof(EmailOutboxMessage),
        nameof(TaskActivity),
        nameof(ScriptExecution),
        nameof(AppUser),
        nameof(AppGroup),
        nameof(AppUserGroup)
    ];

    /// <summary>Audit ciktisina yazilmayacak alanlar.</summary>
    private static readonly HashSet<string> ExcludedProperties =
    [
        nameof(AppUser.Photo),
        nameof(AuditableEntity.CreatedAt),
        nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAt),
        nameof(AuditableEntity.UpdatedBy),
        nameof(Runbook.RowVersion)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var userName = currentUser.UserName;
        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy = userName;
                }
                else
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userName;
                    // Olusturma bilgisi guncellemelerde degistirilemez.
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                }
            }

            var typeName = entry.Metadata.ClrType.Name;
            if (ExcludedTypes.Contains(typeName))
            {
                continue;
            }

            var log = BuildAuditLog(entry, typeName, userName, now);
            if (log is not null)
            {
                auditEntries.Add(log);
            }
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }
    }

    private AuditLog? BuildAuditLog(EntityEntry entry, string typeName, string userName, DateTimeOffset now)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Create,
            EntityState.Modified => AuditAction.Update,
            EntityState.Deleted => AuditAction.Delete,
            _ => (AuditAction?)null
        };

        if (action is null)
        {
            return null;
        }

        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (ExcludedProperties.Contains(name) || property.Metadata.ClrType == typeof(byte[]))
            {
                continue;
            }

            switch (action)
            {
                case AuditAction.Create when property.CurrentValue is not null:
                    changes[name] = property.CurrentValue;
                    break;
                case AuditAction.Update when property.IsModified &&
                                             !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new { Old = property.OriginalValue, New = property.CurrentValue };
                    break;
                case AuditAction.Delete when property.Metadata.IsPrimaryKey():
                    changes[name] = property.OriginalValue;
                    break;
            }
        }

        if (action == AuditAction.Update && changes.Count == 0)
        {
            return null;
        }

        return new AuditLog
        {
            Timestamp = now,
            UserName = userName,
            UserDisplayName = currentUser.DisplayName,
            Action = action.Value,
            EntityType = typeName,
            EntityId = TryGetKey(entry),
            RunbookId = TryGetRunbookId(entry),
            Changes = JsonSerializer.Serialize(changes, JsonOptions),
            Summary = $"{typeName} {action}",
            IpAddress = currentUser.IpAddress
        };
    }

    private static string? TryGetKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }

        var values = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
            .Where(v => v is not null);

        return string.Join('|', values);
    }

    /// <summary>Audit kaydini runbook'a baglar; audit ekraninda filtrelemeyi mumkun kilar.</summary>
    private static Guid? TryGetRunbookId(EntityEntry entry) => entry.Entity switch
    {
        Runbook runbook => runbook.Id,
        RunbookTask task => task.RunbookId,
        TaskAssignment assignment => assignment.Task?.RunbookId,
        TaskComment comment => comment.Task?.RunbookId,
        RunbookScript script => script.RunbookId,
        _ => null
    };
}
