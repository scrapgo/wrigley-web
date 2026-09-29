namespace ScrapGo.Core.Modules.Identity.Domain.Organizations;

/// <summary>Stored as <c>text</c> plus a named CHECK constraint (<c>ck_organizations_status</c>).</summary>
public enum OrganizationStatus
{
    Active,

    /// <summary>Soft-deleted: excluded from "my organizations", but its rows are kept.</summary>
    Disabled,
}
