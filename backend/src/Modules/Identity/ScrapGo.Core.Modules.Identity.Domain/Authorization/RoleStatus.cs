namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>Stored as <c>text</c> plus a named CHECK constraint (<c>ck_roles_status</c>).</summary>
public enum RoleStatus
{
    Active,

    /// <summary>Soft-deleted. Grants nothing, and can no longer be edited.</summary>
    Deleted,
}
