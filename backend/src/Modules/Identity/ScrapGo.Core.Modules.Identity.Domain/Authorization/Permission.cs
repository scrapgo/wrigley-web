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

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
