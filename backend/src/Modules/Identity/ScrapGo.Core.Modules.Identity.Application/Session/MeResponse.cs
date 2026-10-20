namespace ScrapGo.Core.Modules.Identity.Application.Session;

/// <summary>The caller as <c>GET /api/me</c> and <c>POST /api/session</c> return them: who they are and what they may use.</summary>
/// <param name="Roles">Role keys, e.g. <c>scrapgo_user</c>.</param>
/// <param name="Permissions">Effective permission keys (granted, and feature on), ordered.</param>
/// <param name="Features">Keys of the features that are on for the caller, ordered.</param>
public sealed record MeResponse(
    MeUserDto User,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Features)
{
    public static MeResponse From(User user, AccessContext access) =>
        new(
            MeUserDto.From(user),
            [.. access.Roles],
            [.. access.Permissions.Order(StringComparer.Ordinal)],
            [.. access.Features.Order(StringComparer.Ordinal)]);
}

/// <param name="IdentityPlatformUid">The GCIP UID (the token's <c>sub</c>).</param>
public sealed record MeUserDto(
    int Id,
    string IdentityPlatformUid,
    string Email,
    string? DisplayName,
    string UserType,
    string Status,
    DateTimeOffset? LastSignInAt)
{
    public static MeUserDto From(User user) =>
        new(user.Id, user.IdentityPlatformUid, user.Email, user.DisplayName, user.UserType.ToString(), user.Status.ToString(), user.LastSignInAt);
}
