using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Builds <c>perm:</c> policies on demand, so no policy is registered per
/// permission: <c>perm:users.view</c> needs <c>users.view</c>, and
/// <c>perm:a|b</c> needs any of <c>a</c> or <c>b</c>. Every other policy name,
/// and the default and fallback policies, come from the default provider.
/// </summary>
/// <remarks>
/// The name format (<see cref="PermissionPolicies"/>) and
/// <see cref="RequirePermissionAttribute"/> live in Shared.Infrastructure, so
/// other modules protect their endpoints without referencing Identity.
/// </remarks>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!PermissionPolicies.TryParse(policyName, out var anyOf))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        if (anyOf.Count == 0)
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(anyOf))
            .Build());
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

/// <summary>Satisfied when the caller holds any of <see cref="AnyOf"/>.</summary>
public sealed record PermissionRequirement(IReadOnlyList<string> AnyOf) : IAuthorizationRequirement;
