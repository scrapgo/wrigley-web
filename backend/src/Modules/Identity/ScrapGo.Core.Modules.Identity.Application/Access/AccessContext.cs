namespace ScrapGo.Core.Modules.Identity.Application.Access;

/// <summary>
/// What one user may do right now (ACCESS-MODEL.md section 8), built by the
/// access rule of section 4:
/// <c>can(user, p) = user is Active AND p is granted by one of their roles AND p's feature is on for them</c>.
/// </summary>
/// <param name="Roles">The keys of the roles the user holds.</param>
/// <param name="Permissions">
/// Effective permission keys: granted by a role and owned by a feature that is
/// on for this user. Empty for a user who isn't <see cref="UserStatus.Active"/>.
/// </param>
/// <param name="Features">Keys of the features that are on for this user.</param>
/// <param name="SupplierRecordIds">The Quickbase supplier Record IDs the user is linked to; empty for internal users.</param>
public sealed record AccessContext(
    int UserId,
    UserType UserType,
    UserStatus Status,
    IReadOnlyCollection<string> Roles,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string> Features,
    IReadOnlySet<int> SupplierRecordIds)
{
    public bool IsActive => Status == UserStatus.Active;

    /// <summary>The access rule for one permission.</summary>
    public bool Can(string permission) => IsActive && Permissions.Contains(permission);
}
