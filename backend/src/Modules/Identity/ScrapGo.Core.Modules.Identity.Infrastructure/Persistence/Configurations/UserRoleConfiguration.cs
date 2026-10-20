using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => ur.Id);

        // Only to filter out the old schema's organization-scoped grants until B6.
        builder.Property<int?>(LegacyScope.Property).HasColumnName(LegacyScope.Column);

        builder.Property(ur => ur.ExpiresAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(ur => ur.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(ur => ur.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(ur => ur.UserId).HasDatabaseName("ix_user_roles_user_id");
        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("ix_user_roles_role_id");

        // A role is held at most once.
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
            .IsUnique()
            .HasDatabaseName(IdentityUniqueConstraints.RoleAssignment);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_user_id");

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_role_id");
    }
}
