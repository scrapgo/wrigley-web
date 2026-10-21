using Microsoft.AspNetCore.Authorization;

namespace ScrapGo.Core.Shared.Infrastructure.Authorization;

/// <summary>
/// Requires the caller to hold at least one of <paramref name="anyOf"/>, by the
/// access rule (an active user, a role that grants it, its feature on for
/// them). Builds the <c>perm:</c> policy the Identity module resolves, so any
/// module can use it without referencing Identity.
/// </summary>
/// <example>
/// <c>[RequirePermission(Permissions.UsersView)]</c>, or
/// <c>[RequirePermission(SupplierPermissions.ViewAll, SupplierPermissions.ViewOwn)]</c> for either.
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
        Policy = PermissionPolicies.Name(anyOf);
    }

    public IReadOnlyList<string> AnyOf { get; }
}
