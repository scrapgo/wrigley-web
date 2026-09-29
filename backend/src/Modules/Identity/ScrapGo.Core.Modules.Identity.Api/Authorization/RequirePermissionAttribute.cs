using Microsoft.AspNetCore.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Requires the caller to hold <see cref="PermissionName"/>.
/// <list type="bullet">
/// <item>By default the check is organization-scoped: the organization comes
/// from the route's <c>{organizationId}</c> value (never from the token), and
/// a route without one is rejected with 400
/// <c>organization_context_required</c>.</item>
/// <item>With <see cref="PlatformScope"/>, only a platform-scoped role
/// assignment (no organization) satisfies it; no organization-level grant can.</item>
/// </list>
/// </summary>
/// <example><c>[RequirePermission(Permissions.InvoiceRead)]</c> on <c>GET api/organizations/{organizationId}/invoices</c>.</example>
/// <remarks>
/// Implements <see cref="IAuthorizationRequirementData"/>, so no named policy
/// has to be registered per permission. Always prefer this over checking role
/// names.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permissionName) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string PermissionName { get; } = permissionName;

    /// <summary>Satisfied only by a platform-scoped role assignment.</summary>
    public bool PlatformScope { get; init; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(PermissionName, PlatformScope)];
}

public sealed record PermissionRequirement(string PermissionName, bool PlatformScope) : IAuthorizationRequirement;
