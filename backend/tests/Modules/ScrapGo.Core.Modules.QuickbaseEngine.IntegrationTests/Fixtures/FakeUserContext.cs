namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Fixtures;

/// <summary>
/// A settable <see cref="IUserContext"/>. The default is an authenticated,
/// active user, so specs that aren't about access can ignore it.
/// </summary>
public sealed class FakeUserContext : IUserContext
{
    public bool IsAuthenticated { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public string? IdentityPlatformUid => IsAuthenticated ? "spec-user" : null;

    public Task<bool> IsActiveUserAsync(CancellationToken cancellationToken) => Task.FromResult(IsAuthenticated && IsActive);

    public Task<bool> HasPermissionAsync(string permissionName, int? organizationId, CancellationToken cancellationToken) =>
        IsActiveUserAsync(cancellationToken);
}
