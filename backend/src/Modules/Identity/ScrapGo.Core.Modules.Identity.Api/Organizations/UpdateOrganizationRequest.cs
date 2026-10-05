namespace ScrapGo.Core.Modules.Identity.Api.Organizations;

/// <summary>
/// Request body for renaming an organization. Nullable for the same reason as
/// every request here: validation is the handler's InvalidRequest check.
/// </summary>
public sealed record UpdateOrganizationRequest(string? Name);
