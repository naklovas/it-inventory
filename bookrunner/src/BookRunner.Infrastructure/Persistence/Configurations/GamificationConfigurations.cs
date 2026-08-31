using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookRunner.Infrastructure.Persistence.Configurations;

public sealed class GamificationEventConfiguration : IEntityTypeConfiguration<GamificationEvent>
{
    public void Configure(EntityTypeBuilder<GamificationEvent> builder)
    {
        builder.ToTable("GamificationEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasConversion<int>();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Runbook>()
            .WithMany()
            .HasForeignKey(e => e.RunbookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RunbookTask>()
            .WithMany()
            .HasForeignKey(e => e.RunbookTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.EventType });
        builder.HasIndex(e => e.CreatedAt);
    }
}

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable("Badges");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code).HasMaxLength(64).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(128).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(512).IsRequired();
        builder.Property(b => b.Icon).HasMaxLength(64).IsRequired();

        builder.HasIndex(b => b.Code).IsUnique();
    }
}

public sealed class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
{
    public void Configure(EntityTypeBuilder<UserBadge> builder)
    {
        builder.ToTable("UserBadges");
        builder.HasKey(ub => ub.Id);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ub => ub.Badge)
            .WithMany()
            .HasForeignKey(ub => ub.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ub => new { ub.UserId, ub.BadgeId }).IsUnique();
    }
}
