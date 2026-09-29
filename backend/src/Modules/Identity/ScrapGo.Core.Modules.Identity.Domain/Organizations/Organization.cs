using System.Text.RegularExpressions;

namespace ScrapGo.Core.Modules.Identity.Domain.Organizations;

/// <summary>
/// A tenant. Every organization-scoped role, role assignment and membership
/// hangs off it. Organizations have no hard-delete path: they are disabled.
/// </summary>
public partial class Organization
{
    private Organization()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>URL-safe identity derived from <see cref="Name"/>. Globally unique (<c>ux_organizations_slug</c>).</summary>
    public string Slug { get; private set; } = string.Empty;

    public OrganizationStatus Status { get; private set; } = OrganizationStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Lower-cases the name and collapses every run of non-alphanumerics to a
    /// single hyphen. Returns an empty string for a name with no letter or
    /// digit, which callers treat as invalid.
    /// </summary>
    public static string ToSlug(string? name) =>
        NonSlugCharacters().Replace((name ?? string.Empty).Trim().ToLowerInvariant(), "-").Trim('-');

    public static Organization Create(string name, DateTimeOffset now)
    {
        var trimmedName = name.Trim();
        var slug = ToSlug(trimmedName);
        if (slug.Length == 0)
        {
            throw new ArgumentException("An organization name must contain at least one letter or digit.", nameof(name));
        }

        return new Organization
        {
            Name = trimmedName,
            Slug = slug,
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool IsActive => Status == OrganizationStatus.Active;

    public void Disable(DateTimeOffset now)
    {
        Status = OrganizationStatus.Disabled;
        UpdatedAt = now;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
