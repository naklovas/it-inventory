using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookRunner.Infrastructure.Persistence.Configurations;

public sealed class RunbookConfiguration : IEntityTypeConfiguration<Runbook>
{
    public void Configure(EntityTypeBuilder<Runbook> builder)
    {
        builder.ToTable("Runbooks");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.Title).HasMaxLength(250).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(8000);
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.TemplateCategory).HasMaxLength(100);
        builder.Property(r => r.ServiceManagerWorkItemId).HasMaxLength(64);
        builder.Property(r => r.Tags).HasMaxLength(1000);
        builder.Property(r => r.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);
        builder.Property(r => r.DeletedBy).HasMaxLength(256);

        // Es zamanli duzenlemede son yazanin digerini ezmesini engeller.
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne(r => r.Owner)
            .WithMany()
            .HasForeignKey(r => r.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SourceTemplate)
            .WithMany()
            .HasForeignKey(r => r.SourceTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IsTemplate, r.Status });
        builder.HasIndex(r => r.ServiceManagerWorkItemId);
        builder.HasIndex(r => r.PlannedStart);

        // Silinen runbook'lar sorgularda otomatik gizlenir.
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

public sealed class RunbookTaskConfiguration : IEntityTypeConfiguration<RunbookTask>
{
    public void Configure(EntityTypeBuilder<RunbookTask> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(250).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(8000);
        builder.Property(t => t.ColorHex).HasMaxLength(9).IsRequired();
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.Priority).HasConversion<int>();
        builder.Property(t => t.RollbackNotes).HasMaxLength(4000);
        builder.Property(t => t.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);
        builder.Property(t => t.DeletedBy).HasMaxLength(256);

        builder.HasOne(t => t.Runbook)
            .WithMany(r => r.Tasks)
            .HasForeignKey(t => t.RunbookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.DependsOnTask)
            .WithMany()
            .HasForeignKey(t => t.DependsOnTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Script)
            .WithMany()
            .HasForeignKey(t => t.ScriptId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.RunbookId, t.Order });
        builder.HasIndex(t => t.Status);

        builder.HasQueryFilter(t => !t.IsDeleted && !t.Runbook.IsDeleted);
    }
}

public sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssigneeType).HasConversion<int>();
        builder.Property(a => a.HandoverNote).HasMaxLength(1000);
        builder.Property(a => a.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        builder.HasOne(a => a.Task)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Group)
            .WithMany()
            .HasForeignKey(a => a.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TaskId, a.IsActive });
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.GroupId);

        // Kisi atamasinda UserId, grup atamasinda GroupId dolu olmalidir.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_TaskAssignments_Target",
            "([AssigneeType] = 0 AND [UserId] IS NOT NULL AND [GroupId] IS NULL) OR " +
            "([AssigneeType] = 1 AND [GroupId] IS NOT NULL AND [UserId] IS NULL)"));

        builder.HasQueryFilter(a => !a.Task.IsDeleted && !a.Task.Runbook.IsDeleted);
    }
}

public sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Body).HasMaxLength(4000).IsRequired();
        builder.Property(c => c.MentionedUserIds).HasMaxLength(2000);
        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
        builder.Property(c => c.DeletedBy).HasMaxLength(256);

        builder.HasOne(c => c.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TaskId, c.CreatedAt });

        builder.HasQueryFilter(c => !c.IsDeleted && !c.Task.IsDeleted && !c.Task.Runbook.IsDeleted);
    }
}

public sealed class TaskActivityConfiguration : IEntityTypeConfiguration<TaskActivity>
{
    public void Configure(EntityTypeBuilder<TaskActivity> builder)
    {
        builder.ToTable("TaskActivities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).HasConversion<int>();
        builder.Property(a => a.ActorDisplayName).HasMaxLength(256).IsRequired();
        builder.Property(a => a.OldValue).HasMaxLength(512);
        builder.Property(a => a.NewValue).HasMaxLength(512);
        builder.Property(a => a.Summary).HasMaxLength(1000).IsRequired();

        builder.HasOne(a => a.Task)
            .WithMany(t => t.Activities)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TaskId, a.CreatedAt });

        builder.HasQueryFilter(a => !a.Task.IsDeleted && !a.Task.Runbook.IsDeleted);
    }
}
