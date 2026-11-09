namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>
/// MFA requirement for external (customer) users: **off** by default. When
/// on, every request from an external user needs either a GCIP second factor
/// on the token (<c>firebase.sign_in_second_factor</c>) or an email code
/// verified for this sign-in (<see cref="Mfa.MfaService"/>), or it is denied
/// with 403 <c>mfa_required</c>. Internal users are never affected: Google
/// Workspace has its own 2-step verification.
/// </summary>
public sealed class ExternalUserMfaOptions
{
    public const string ConfigurationKey = "Identity:RequireMfaForExternalUsers";

    public bool RequireForExternalUsers { get; set; }
}
