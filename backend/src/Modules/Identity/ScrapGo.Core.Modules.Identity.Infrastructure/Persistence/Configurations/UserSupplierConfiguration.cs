using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserSupplierConfiguration : IEntityTypeConfiguration<UserSupplier>
{
    public void Configure(EntityTypeBuilder<UserSupplier> builder)
    {
        builder.ToTable("user_suppliers");

        // Compound key: a user is linked to a supplier at most once.
        builder.HasKey(us => new { us.UserId, us.SupplierRecordId });

        builder.Property(us => us.UserType)
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(us => us.LinkedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(us => us.LinkedByUserId)
            .IsRequired(false);

        builder.HasIndex(us => us.SupplierRecordId).HasDatabaseName("ix_user_suppliers_supplier_record_id");

        // Only External users have supplier links; internal users see every supplier.
        builder.ToTable(t => t.HasCheckConstraint("ck_user_suppliers_external", "user_type = 'External'"));

        // With the CHECK above, this composite key makes the database refuse a
        // link for an internal user.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(us => new { us.UserId, us.UserType })
            .HasPrincipalKey(u => new { u.Id, u.UserType })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_suppliers_user_id_user_type");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(us => us.LinkedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_user_suppliers_linked_by_user_id");
    }
}
