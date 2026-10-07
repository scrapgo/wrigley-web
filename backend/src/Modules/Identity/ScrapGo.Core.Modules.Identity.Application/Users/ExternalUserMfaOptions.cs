namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>
/// Reserved MFA requirement for external (customer) users
/// (ORG-APP-MODULE-MODEL.md, Decision 4b): **off** until MFA is enabled in the
/// Identity Platform project. When on, an external user's token must show a
/// second factor (<c>firebase.sign_in_second_factor</c>) on every request, or
/// the request is denied with 403 <c>mfa_required</c>. Internal users are never
/// affected.
/// </summary>
public sealed class ExternalUserMfaOptions
{
    public const string ConfigurationKey = "Identity:RequireMfaForExternalUsers";

    public bool RequireForExternalUsers { get; set; }
}
