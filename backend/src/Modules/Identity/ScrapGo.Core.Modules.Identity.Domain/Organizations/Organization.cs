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

    /// <summary>
    /// Changes the display name only. The slug is an identifier and stays as
    /// created, so renaming never breaks a link or collides with another slug.
    /// </summary>
    public void Rename(string name, DateTimeOffset now)
    {
        var trimmedName = name.Trim();
        if (ToSlug(trimmedName).Length == 0)
        {
            throw new ArgumentException("An organization name must contain at least one letter or digit.", nameof(name));
        }

        Name = trimmedName;
        UpdatedAt = now;
    }

    /// <summary>
    /// Renames and regenerates the slug from the new name: the platform admin's
    /// correction of a mistaken name. Routes use the id, never the slug, so no
    /// link breaks; uniqueness is enforced by the database on save.
    /// </summary>
    public void RenameWithSlug(string name, DateTimeOffset now)
    {
        Rename(name, now);
        Slug = ToSlug(Name);
    }

    /// <summary>
    /// Deactivates the organization: every organization- and application-scoped
    /// check then denies its members. Nothing is deleted, so
    /// <see cref="Reactivate"/> restores everything as it was.
    /// </summary>
    public void Disable(DateTimeOffset now)
    {
        Status = OrganizationStatus.Disabled;
        UpdatedAt = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        Status = OrganizationStatus.Active;
        UpdatedAt = now;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
