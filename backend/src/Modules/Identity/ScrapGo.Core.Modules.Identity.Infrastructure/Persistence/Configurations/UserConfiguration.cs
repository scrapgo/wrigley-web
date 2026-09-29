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

        // Status enums are text + HasConversion<string> + an explicit CHECK
        // constraint rather than native Postgres enum types: readable in raw
        // SQL, and adding a value is a constraint change, not an ALTER TYPE.
        builder.Property(u => u.Status)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(UserStatus.Active);

        builder.Property(u => u.Classification)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(UserClassification.External);

        builder.Property(u => u.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(u => u.IsActive);

        // Identity is always resolved by UID, so this index carries that
        // lookup traffic. It is also the arbiter UserRepository's
        // ON CONFLICT insert relies on for exactly-once provisioning.
        builder.HasIndex(u => u.IdentityPlatformUid)
            .IsUnique()
            .HasDatabaseName("ux_users_identity_platform_uid");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("ix_users_status");

        builder.HasIndex(u => u.Classification)
            .HasDatabaseName("ix_users_classification");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_users_status", "status IN ('Active', 'Disabled')");
            t.HasCheckConstraint("ck_users_classification", "classification IN ('External', 'Internal')");
        });
    }
}
