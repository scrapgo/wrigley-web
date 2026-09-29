namespace ScrapGo.Core.Modules.Identity.Domain.Users;

public enum LinkedCredentialStatus
{
    /// <summary>Usable: GCIP-native, or tied to a still-existing organization identity provider.</summary>
    Active,

    /// <summary>
    /// The organization identity provider this credential was tied to has
    /// been removed. There are no cascade deletes: re-linking or
    /// admin-assisted migration is required.
    /// </summary>
    Orphaned,
}
