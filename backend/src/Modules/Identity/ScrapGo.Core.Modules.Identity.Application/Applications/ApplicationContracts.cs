using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Applications;

/// <summary>A catalog module with the permissions it owns.</summary>
public sealed record CatalogModuleDto(int Id, string Key, string Name, string Status, IReadOnlyList<string> Permissions);

/// <summary>A catalog application with its modules.</summary>
public sealed record CatalogApplicationDto(int Id, string Key, string Name, string Status, IReadOnlyList<CatalogModuleDto> Modules);

/// <summary>A module that is on for an organization's application.</summary>
public sealed record OrganizationModuleDto(int ModuleId, string Key, string Name);

/// <summary>An application assigned to an organization, with its enabled modules only.</summary>
public sealed record OrganizationApplicationDto(
    int ApplicationId, string Key, string Name, DateTimeOffset EnabledAt, IReadOnlyList<OrganizationModuleDto> Modules);

/// <summary>One member's access to one application in one organization (access review).</summary>
/// <param name="Roles">Their unexpired application grants there.</param>
/// <param name="Permissions">What those grants resolve to now: enabled modules only.</param>
public sealed record AccessReviewEntryDto(int UserId, string Email, IReadOnlyList<AssignedRoleDto> Roles, IReadOnlyList<string> Permissions);

/// <summary>A user's access within one application, for UI gating on <c>/me</c>.</summary>
/// <param name="Modules">Enabled modules in which the user holds at least one permission.</param>
public sealed record ApplicationAccessDto(
    int ApplicationId, string Key, string Name, IReadOnlyList<OrganizationModuleDto> Modules, IReadOnlyList<string> Permissions);

/// <summary>A user's access within one organization, for UI gating on <c>/me</c>.</summary>
/// <param name="Permissions">Organization-level permissions (identity administration).</param>
/// <param name="Applications">Applications in which the user holds at least one permission.</param>
public sealed record OrganizationAccessDto(
    int OrganizationId, string Name, IReadOnlyList<string> Permissions, IReadOnlyList<ApplicationAccessDto> Applications);

/// <summary>An application grant to read back: who holds which application role, until when.</summary>
public sealed record ApplicationGrantHolder(int UserId, string Email, AssignedRoleDto Role);
