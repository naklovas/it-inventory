using BookRunner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookRunner.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Sid).HasMaxLength(184).IsRequired();
        builder.HasIndex(u => u.Sid).IsUnique();

        builder.Property(u => u.SamAccountName).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.SamAccountName);

        builder.Property(u => u.UserPrincipalName).HasMaxLength(256);
        builder.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.Title).HasMaxLength(128);
        builder.Property(u => u.Department).HasMaxLength(128);
        builder.Property(u => u.Company).HasMaxLength(128);
        builder.Property(u => u.OfficePhone).HasMaxLength(64);
        builder.Property(u => u.MobilePhone).HasMaxLength(64);
        builder.Property(u => u.ManagerDistinguishedName).HasMaxLength(512);
        builder.Property(u => u.DistinguishedName).HasMaxLength(512);
        builder.Property(u => u.PhotoContentType).HasMaxLength(64);
        builder.Property(u => u.PhotoHash).HasMaxLength(64);
        builder.Property(u => u.Initials).HasMaxLength(4).IsRequired();
        builder.Property(u => u.AvatarColor).HasMaxLength(9).IsRequired();

        // Fotograf satirlarin cogunda okunmaz; ayri sorguyla getirilir.
        builder.Property(u => u.Photo).HasColumnType("varbinary(max)");

        builder.HasIndex(u => u.DisplayName);
    }
}

public sealed class AppGroupConfiguration : IEntityTypeConfiguration<AppGroup>
{
    public void Configure(EntityTypeBuilder<AppGroup> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Sid).HasMaxLength(184).IsRequired();
        builder.HasIndex(g => g.Sid).IsUnique();

        builder.Property(g => g.Name).HasMaxLength(256).IsRequired();
        builder.Property(g => g.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(1024);
        builder.Property(g => g.Email).HasMaxLength(256);
        builder.Property(g => g.DistinguishedName).HasMaxLength(512);
        builder.Property(g => g.AvatarColor).HasMaxLength(9).IsRequired();

        builder.HasIndex(g => g.Name);
    }
}

public sealed class AppUserGroupConfiguration : IEntityTypeConfiguration<AppUserGroup>
{
    public void Configure(EntityTypeBuilder<AppUserGroup> builder)
    {
        builder.ToTable("UserGroups");
        builder.HasKey(ug => new { ug.UserId, ug.GroupId });

        builder.HasOne(ug => ug.User)
            .WithMany(u => u.Groups)
            .HasForeignKey(ug => ug.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ug => ug.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(ug => ug.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ug => ug.GroupId);
    }
}

public sealed class RoleMappingConfiguration : IEntityTypeConfiguration<RoleMapping>
{
    public void Configure(EntityTypeBuilder<RoleMapping> builder)
    {
        builder.ToTable("RoleMappings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TeamName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Role).HasConversion<int>();
        builder.Property(r => r.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(r => new { r.TeamName, r.Role }).IsUnique();
    }
}
