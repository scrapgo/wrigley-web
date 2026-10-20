using Microsoft.AspNetCore.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Requires the caller to hold at least one of <paramref name="anyOf"/>, by the
/// access rule (an active user, a role that grants it, its feature on for
/// them). Builds the <c>perm:</c> policy <see cref="PermissionPolicyProvider"/>
/// resolves.
/// </summary>
/// <example>
/// <c>[RequirePermission(Permissions.UsersView)]</c>, or
/// <c>[RequirePermission(Permissions.SuppliersViewAll, Permissions.SuppliersViewOwn)]</c> for either.
/// </example>
/// <remarks>Always prefer this over checking role names: code checks permission keys only.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(params string[] anyOf)
    {
        if (anyOf.Length == 0 || anyOf.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-blank permission key is required.", nameof(anyOf));
        }

        AnyOf = anyOf;
        Policy = PermissionPolicyProvider.PolicyName(anyOf);
    }

    public IReadOnlyList<string> AnyOf { get; }
}
