namespace ScrapGo.Core.Modules.Identity.Domain.Applications;

/// <summary>Stored as <c>text</c> plus a named CHECK constraint.</summary>
public enum CatalogStatus
{
    Active,

    /// <summary>Withdrawn by the platform: resolves to deny everywhere until reactivated. Rows are never deleted.</summary>
    Retired,
}

/// <summary>
/// A platform application in the global catalog (e.g. Price Optimizer). It's
/// defined in code (<see cref="ApplicationCatalog"/>) and seeded by migration;
/// only <see cref="Status"/> changes at runtime.
/// </summary>
/// <remarks>Named <c>CatalogApplication</c> so it never collides with the <c>…Identity.Application</c> namespace.</remarks>
public class CatalogApplication
{
    private CatalogApplication()
    {
    }

    public int Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public CatalogStatus Status { get; private set; } = CatalogStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == CatalogStatus.Active;

    public void ChangeStatus(CatalogStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }
}

/// <summary>A module of a catalog application (e.g. Invoices). It owns its permissions.</summary>
public class CatalogModule
{
    private CatalogModule()
    {
    }

    public int Id { get; private set; }

    public int ApplicationId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public CatalogStatus Status { get; private set; } = CatalogStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == CatalogStatus.Active;

    public void ChangeStatus(CatalogStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }
}
