namespace ScrapGo.Core.Modules.Identity.Api.Users;

/// <summary>
/// Request body for assigning a role to a user. <see cref="OrganizationId"/>
/// null means platform scope. Both are nullable so a malformed body reaches
/// the handler's InvalidRequest check (400 <c>invalid_request</c>) instead of
/// the framework's own validation response.
/// </summary>
public sealed record AssignRoleRequest(int? RoleId, int? OrganizationId);
