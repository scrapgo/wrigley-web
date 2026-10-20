namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// One discrete, named <c>resource.action</c> permission, owned by exactly one
/// <see cref="Features.Feature"/>. The set is a compile-time contract seeded by
/// migration: admins map keys to roles, they never invent keys.
/// </summary>
public class Permission
{
    private Permission()
    {
    }

    public int Id { get; private set; }

    /// <summary>Unique, e.g. <c>suppliers.view_all</c>.</summary>
    public string Key { get; private set; } = string.Empty;

    public int FeatureId { get; private set; }

    public PermissionAudience Audience { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public static Permission Create(string key, int featureId, PermissionAudience audience, string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Permission
        {
            Key = key,
            FeatureId = featureId,
            Audience = audience,
            Name = name,
            Description = description ?? string.Empty,
        };
    }
}
