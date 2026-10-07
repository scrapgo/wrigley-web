namespace ScrapGo.Core.Modules.Identity.Domain.Applications;

public enum OrganizationApplicationStatus
{
    Active,

    /// <summary>Removed by the platform. The row stays, so re-assigning keeps its module selections.</summary>
    Removed,
}

public enum ModuleEnablementStatus
{
    Enabled,

    /// <summary>The module's permissions stop resolving; grants are kept and return on re-enable.</summary>
    Disabled,
}

/// <summary>
/// Tenant entitlement: an organization has an application. Soft state only:
/// removing and re-assigning flips <see cref="Status"/> on the same row.
/// </summary>
public class OrganizationApplication
{
    private OrganizationApplication()
    {
    }

    public int Id { get; private set; }

    public int OrganizationId { get; private set; }

    public int ApplicationId { get; private set; }

    public OrganizationApplicationStatus Status { get; private set; } = OrganizationApplicationStatus.Active;

    /// <summary>When the application was (last) assigned.</summary>
    public DateTimeOffset EnabledAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == OrganizationApplicationStatus.Active;

    public static OrganizationApplication Assign(int organizationId, int applicationId, DateTimeOffset now) =>
        new()
        {
            OrganizationId = organizationId,
            ApplicationId = applicationId,
            Status = OrganizationApplicationStatus.Active,
            EnabledAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Reassign(DateTimeOffset now)
    {
        Status = OrganizationApplicationStatus.Active;
        EnabledAt = now;
        RemovedAt = null;
        UpdatedAt = now;
    }

    public void Remove(DateTimeOffset now)
    {
        Status = OrganizationApplicationStatus.Removed;
        RemovedAt = now;
        UpdatedAt = now;
    }
}

/// <summary>
/// Tenant entitlement: a module is on for one organization's application.
/// Licensed, so only platform administrators toggle it (Decision 6).
/// </summary>
public class OrganizationApplicationModule
{
    private OrganizationApplicationModule()
    {
    }

    public int Id { get; private set; }

    public int OrganizationApplicationId { get; private set; }

    /// <summary>Redundant with the organization application's, so composite FKs can prove the module belongs to that application.</summary>
    public int ApplicationId { get; private set; }

    public int ModuleId { get; private set; }

    public ModuleEnablementStatus Status { get; private set; } = ModuleEnablementStatus.Enabled;

    public DateTimeOffset EnabledAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsEnabled => Status == ModuleEnablementStatus.Enabled;

    public static OrganizationApplicationModule Enable(int organizationApplicationId, int applicationId, int moduleId, DateTimeOffset now) =>
        new()
        {
            OrganizationApplicationId = organizationApplicationId,
            ApplicationId = applicationId,
            ModuleId = moduleId,
            Status = ModuleEnablementStatus.Enabled,
            EnabledAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Reenable(DateTimeOffset now)
    {
        Status = ModuleEnablementStatus.Enabled;
        EnabledAt = now;
        DisabledAt = null;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        Status = ModuleEnablementStatus.Disabled;
        DisabledAt = now;
        UpdatedAt = now;
    }
}
