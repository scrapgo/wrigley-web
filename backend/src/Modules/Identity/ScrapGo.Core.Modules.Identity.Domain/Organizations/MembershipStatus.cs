namespace ScrapGo.Core.Modules.Identity.Domain.Organizations;

/// <summary>Stored as <c>text</c> plus a named CHECK constraint (<c>ck_organization_memberships_status</c>).</summary>
public enum MembershipStatus
{
    Active,

    /// <summary>The user keeps the row but loses every organization-scoped right in that organization.</summary>
    Disabled,
}
