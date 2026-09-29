namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>Grants <see cref="Permission"/> to <see cref="Role"/>. Keyed by (role, permission).</summary>
public class RolePermission
{
    private RolePermission()
    {
    }

    public int RoleId { get; private set; }

    public int PermissionId { get; private set; }

    public Permission Permission { get; private set; } = null!;

    public static RolePermission Create(int roleId, int permissionId) =>
        new() { RoleId = roleId, PermissionId = permissionId };
}
