using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        // Compound key: a user holds a role at most once.
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserType)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(ur => ur.AssignedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(ur => ur.AssignedByUserId)
            .IsRequired(false);

        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("ix_user_roles_role_id");

        builder.ToTable(t => t.HasCheckConstraint("ck_user_roles_user_type", "user_type IN ('Internal', 'External')"));

        // The two composite foreign keys share user_type, so the database
        // refuses a role held by a user of the other type.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ur => new { ur.UserId, ur.UserType })
            .HasPrincipalKey(u => new { u.Id, u.UserType })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_roles_user_id_user_type");

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => new { ur.RoleId, ur.UserType })
            .HasPrincipalKey(r => new { r.Id, r.UserType })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_roles_role_id_user_type");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ur => ur.AssignedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_user_roles_assigned_by_user_id");
    }
}
