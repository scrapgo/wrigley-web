namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// One entry in the permission catalog. The catalog is a compile-time
/// contract (<see cref="Permissions"/>), seeded once by migration and never
/// written over HTTP.
/// </summary>
public class Permission
{
    private Permission()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The catalog module that owns it, or null for an identity/administration
    /// permission (<c>User.*</c>, <c>Role.*</c>, <c>Admin.Access</c>, …).
    /// </summary>
    public int? ModuleId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
