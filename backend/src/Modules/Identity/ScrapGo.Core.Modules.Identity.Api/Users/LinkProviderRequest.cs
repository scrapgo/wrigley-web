namespace ScrapGo.Core.Modules.Identity.Api.Users;

/// <summary>
/// Request body for <c>POST /api/users/me/linked-providers/link</c>.
/// <see cref="ReauthIdToken"/> is a second GCIP ID token, independent of the
/// request's bearer token, minted moments earlier by GCIP's client-side
/// re-authentication/link flow.
/// </summary>
public sealed record LinkProviderRequest(string? ReauthIdToken);
