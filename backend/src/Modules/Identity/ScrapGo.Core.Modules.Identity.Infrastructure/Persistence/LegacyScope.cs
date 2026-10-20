namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Temporary, until B6 resets the Identity schema: the old <c>roles</c> and
/// <c>user_roles</c> tables still hold organization-scoped custom roles and
/// grants. Roles are global now, so those rows must never resolve, list or be
/// assigned. The column is mapped as a shadow property only to filter them out.
/// </summary>
internal static class LegacyScope
{
    /// <summary>The shadow property, mapped to the old <c>organization_id</c> column.</summary>
    public const string Property = "LegacyScope";

    public const string Column = "organization_id";
}
