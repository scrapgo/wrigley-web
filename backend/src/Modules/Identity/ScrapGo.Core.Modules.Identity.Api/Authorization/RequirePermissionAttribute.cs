using Microsoft.AspNetCore.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Requires the caller to hold <see cref="PermissionName"/>. Roles are global,
/// so the check has no scope; it also needs a Google Workspace sign-in on the
/// request (403 <c>workspace_sign_in_required</c> otherwise).
/// </summary>
/// <example><c>[RequirePermission(Permissions.UsersView)]</c> on <c>GET api/users</c>.</example>
/// <remarks>
/// Implements <see cref="IAuthorizationRequirementData"/>, so no named policy
/// has to be registered per permission. Always prefer this over checking role
/// names.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permissionName) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string PermissionName { get; } = permissionName;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(PermissionName)];
}

public sealed record PermissionRequirement(string PermissionName) : IAuthorizationRequirement;
