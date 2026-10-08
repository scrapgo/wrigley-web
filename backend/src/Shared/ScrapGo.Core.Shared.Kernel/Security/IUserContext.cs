namespace ScrapGo.Core.Shared.Kernel.Security;

/// <summary>
/// The current caller as the Identity module knows them. Any module checks
/// this before touching protected data. In particular, every Quickbase access
/// goes through it (AGENTS.md: "Every request must be checked against the
/// UserContext (Identity/Permissions) before accessing Quickbase data").
/// </summary>
/// <remarks>
/// A contract in Shared.Kernel, implemented by the Identity module, so other
/// modules depend on the concept and never on Identity's projects.
/// </remarks>
public interface IUserContext
{
    /// <summary>True when the request carries a validated GCIP bearer token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The validated token's <c>sub</c>, or null when unauthenticated.</summary>
    string? IdentityPlatformUid { get; }

    /// <summary>True only for an authenticated caller with a provisioned user row whose status is Active.</summary>
    Task<bool> IsActiveUserAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the caller holds <paramref name="permissionName"/> in
    /// <paramref name="organizationId"/>, or at platform scope when it is null.
    /// Always false for an unauthenticated or inactive caller.
    /// </summary>
    Task<bool> HasPermissionAsync(string permissionName, int? organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the caller holds <paramref name="permissionName"/> for
    /// <paramref name="applicationId"/> within <paramref name="organizationId"/>
    /// (application scope). That needs an active membership, the application
    /// assigned to the organization, and, for a module permission, the module
    /// enabled there. Always false for an unauthenticated or inactive caller.
    /// </summary>
    Task<bool> HasApplicationPermissionAsync(
        string permissionName, int organizationId, int applicationId, CancellationToken cancellationToken);
}
