namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// Whether a <see cref="User"/> belongs to the operating organization. Decided
/// once, at first provisioning, from the token's <c>hd</c> claim against the
/// <c>INTERNAL_HD_ALLOWLIST</c> configuration, and never re-evaluated (fail
/// closed).
/// </summary>
public enum UserClassification
{
    /// <summary>
    /// The default: the <c>hd</c> claim was not on the allow-list, or was
    /// absent (personal account). Never eligible for platform-scoped roles.
    /// </summary>
    External,

    /// <summary>The <c>hd</c> claim matched the allow-list at provisioning time.</summary>
    Internal,
}
