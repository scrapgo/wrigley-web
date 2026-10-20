using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Builds <c>perm:</c> policies on demand, so no policy is registered per
/// permission: <c>perm:users.view</c> needs <c>users.view</c>, and
/// <c>perm:a|b</c> needs any of <c>a</c> or <c>b</c>. Every other policy name,
/// and the default and fallback policies, come from the default provider.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    public const string Prefix = "perm:";

    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public static string PolicyName(IEnumerable<string> anyOf) => Prefix + string.Join('|', anyOf);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var anyOf = policyName[Prefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (anyOf.Length == 0)
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
