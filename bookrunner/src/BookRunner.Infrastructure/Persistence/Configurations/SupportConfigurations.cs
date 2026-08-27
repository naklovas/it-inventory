using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookRunner.Infrastructure.Persistence.Configurations;

public sealed class RunbookScriptConfiguration : IEntityTypeConfiguration<RunbookScript>
{
    public void Configure(EntityTypeBuilder<RunbookScript> builder)
    {
        builder.ToTable("Scripts");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.Code).IsRequired();
        builder.Property(s => s.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        builder.HasOne(s => s.Runbook)
            .WithMany(r => r.Scripts)
            .HasForeignKey(s => s.RunbookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.Name);
    }
}

public sealed class ScriptExecutionConfiguration : IEntityTypeConfiguration<ScriptExecution>
{
    public void Configure(EntityTypeBuilder<ScriptExecution> builder)
    {
        builder.ToTable("ScriptExecutions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<int>();

        builder.HasOne(e => e.Script)
            .WithMany(s => s.Executions)
            .HasForeignKey(e => e.ScriptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Task)
            .WithMany()
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ExecutedBy)
            .WithMany()
            .HasForeignKey(e => e.ExecutedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.StartedAt);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserName).HasMaxLength(256).IsRequired();
        builder.Property(a => a.UserDisplayName).HasMaxLength(256);
        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Summary).HasMaxLength(1000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.RunbookId);
        builder.HasIndex(a => a.UserName);
    }
}

public sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutbox");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.To).HasMaxLength(2000).IsRequired();
        builder.Property(m => m.Cc).HasMaxLength(2000);
        builder.Property(m => m.Subject).HasMaxLength(500).IsRequired();
        builder.Property(m => m.HtmlBody).IsRequired();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.Reason).HasMaxLength(100);

        // Arka plan gonderici bu indeks uzerinden bekleyen mesajlari cekiyor.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
    }
}
