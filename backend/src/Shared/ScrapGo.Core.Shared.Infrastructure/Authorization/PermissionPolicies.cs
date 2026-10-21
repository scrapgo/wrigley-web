namespace ScrapGo.Core.Shared.Infrastructure.Authorization;

/// <summary>
/// The <c>perm:</c> policy-name format every module's
/// <see cref="RequirePermissionAttribute"/> writes and the Identity module's
/// policy provider reads: <c>perm:users.view</c>, or <c>perm:a|b</c> for any of.
/// </summary>
public static class PermissionPolicies
{
    public const string Prefix = "perm:";

    public const char Separator = '|';

    public static string Name(IEnumerable<string> anyOf) => Prefix + string.Join(Separator, anyOf);

    /// <summary>The permission keys in a <c>perm:</c> policy name; false for any other name.</summary>
    public static bool TryParse(string policyName, out IReadOnlyList<string> anyOf)
    {
        anyOf = [];
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        anyOf = policyName[Prefix.Length..].Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return true;
    }
}
