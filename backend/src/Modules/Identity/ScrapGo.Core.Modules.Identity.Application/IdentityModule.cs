namespace ScrapGo.Core.Modules.Identity.Application;

/// <summary>
/// Marker type for this module. It scopes module-bound shared services such
/// as <see cref="IAuditLog{TModule}"/> to the Identity module's own DbContext.
/// </summary>
public sealed class IdentityModule
{
    private IdentityModule()
    {
    }
}
