using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.IdentityPlatformUid)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasColumnType("text")
            .IsRequired(false);

        // Status enums are text + HasConversion<string> + an explicit CHECK
        // constraint rather than native Postgres enum types: readable in raw
        // SQL, and adding a value is a constraint change, not an ALTER TYPE.
        builder.Property(u => u.UserType)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(u => u.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(UserStatus.Active);

        builder.Property(u => u.SignInProvider)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(u => u.LastSignInAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(u => u.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(u => u.IsActive);

        // The target of the composite foreign keys (user_id, user_type) from
        // user_roles and user_suppliers, which keep roles and supplier links to
        // users of the right type. It also makes user_type immutable in EF.
        builder.HasAlternateKey(u => new { u.Id, u.UserType })
            .HasName("ux_users_id_user_type");

        // Identity is always resolved by UID, so this index carries that
        // lookup traffic. It is also the arbiter UserRepository's
        // ON CONFLICT insert relies on for exactly-once provisioning.
        builder.HasIndex(u => u.IdentityPlatformUid)
            .IsUnique()
            .HasDatabaseName("ux_users_identity_platform_uid");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("ix_users_status");

        builder.HasIndex(u => u.UserType)
            .HasDatabaseName("ix_users_user_type");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_users_status", "status IN ('Active', 'Disabled')");
            t.HasCheckConstraint("ck_users_user_type", "user_type IN ('Internal', 'External')");
        });
    }
}
