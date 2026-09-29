namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>The wire shape of the caller's own user record. Entities never leave this layer.</summary>
public sealed record CurrentUserDto(int Id, string IdentityPlatformUid, string Email, string Status, string Classification)
{
    public static CurrentUserDto From(User user) =>
        new(user.Id, user.IdentityPlatformUid, user.Email, user.Status.ToString(), user.Classification.ToString());
}
