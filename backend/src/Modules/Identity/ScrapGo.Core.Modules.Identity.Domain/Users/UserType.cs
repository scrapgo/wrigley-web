namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// Whether a <see cref="User"/> is ScrapGo staff or an outside party. Set once,
/// when the user is created, and never re-evaluated (ACCESS-MODEL.md section 3).
/// A role may only be held by a user of the same type.
/// </summary>
public enum UserType
{
    /// <summary>
    /// A Google sign-in with a verified email whose <c>hd</c> is on
    /// <c>INTERNAL_HD_ALLOWLIST</c>.
    /// </summary>
    Internal,

    /// <summary>Buyers, sellers and transporters, who join by invitation only.</summary>
    External,
}
