namespace ScrapGo.Core.Modules.Identity.Domain.Organizations;

/// <summary>
/// A user's membership in an <see cref="Organization"/>, unique per
/// (user, organization). An active membership is the precondition for every
/// organization-scoped access decision: roles held in an organization mean
/// nothing without it.
/// </summary>
public class OrganizationMembership
{
    private OrganizationMembership()
    {
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    public int OrganizationId { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public MembershipStatus Status { get; private set; } = MembershipStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrganizationMembership Create(int userId, int organizationId, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId,
            Status = MembershipStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Disable(DateTimeOffset now)
    {
        Status = MembershipStatus.Disabled;
        UpdatedAt = now;
    }
}
