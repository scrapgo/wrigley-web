namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// Links an External user to one Quickbase supplier they may see. Keyed by
/// (user, supplier). Internal users see every supplier and never have links;
/// the database enforces that through <see cref="UserType"/>.
/// </summary>
public class UserSupplier
{
    private UserSupplier()
    {
    }

    public int UserId { get; private set; }

    /// <summary>Always <see cref="UserType.External"/>; part of the composite foreign key to the user.</summary>
    public UserType UserType { get; private set; } = UserType.External;

    /// <summary>The Quickbase Suppliers table's Record ID#.</summary>
    public int SupplierRecordId { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    /// <summary>Who made the link; null when the system made it (e.g. accepting an invitation).</summary>
    public int? LinkedByUserId { get; private set; }

    public static UserSupplier Link(int userId, int supplierRecordId, int? linkedByUserId, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            UserType = UserType.External,
            SupplierRecordId = supplierRecordId,
            LinkedAt = now,
            LinkedByUserId = linkedByUserId,
        };
}
