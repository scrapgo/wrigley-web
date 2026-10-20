namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Which roles a <see cref="Permission"/> may be mapped to: internal roles
/// only, external roles only, or either. Enforced in the Application layer.
/// </summary>
public enum PermissionAudience
{
    Internal,

    External,

    Any,
}
