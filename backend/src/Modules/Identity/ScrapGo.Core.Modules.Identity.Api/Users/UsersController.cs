using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Domain.Users;
using ScrapGo.Core.Shared.Kernel.Paging;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Users;

/// <param name="RoleIds">Every role the user should hold; roles not listed are removed.</param>
public sealed record SetUserRolesRequest(IReadOnlyList<int>? RoleIds);

/// <param name="SupplierRecordIds">Every Quickbase supplier Record ID the user should be linked to; others are unlinked.</param>
public sealed record SetUserSuppliersRequest(IReadOnlyList<int>? SupplierRecordIds);

/// <summary>
/// User administration (ACCESS-MODEL.md section 9). Reads need
/// <c>users.view</c>; changes need <c>users.manage</c>. This controller parses
/// the request, calls one Application service and maps the outcome; the rules
/// live in <see cref="UserAdminService"/>.
/// </summary>
/// <remarks>The caller's own record is <c>GET /api/me</c>.</remarks>
[ApiController]
[Authorize]
[Route("api/admin/users")]
[Produces("application/json")]
public sealed class UsersController(UserReadService reads, UserAdminService admin) : ControllerBase
{
    /// <summary>
    /// Users, paged by id. Filters: <c>search</c> (email or display name
    /// substring), <c>type</c> (Internal, External), <c>status</c> (Active, Disabled).
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.UsersView)]
    [ProducesResponseType<PagedResult<UserSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        await reads.ListAsync(new ListUsersQuery(search, type, status, page, pageSize), cancellationToken) is { } users
            ? Ok(users)
            : InvalidRequest(
                $"page must be 1 or more, pageSize 1 to {PageRequest.MaxPageSize}, type one of: {string.Join(", ", Enum.GetNames<UserType>())}, " +
                $"and status one of: {string.Join(", ", Enum.GetNames<UserStatus>())}.");

    /// <summary>One user, their roles and their supplier links.</summary>
    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCatalog.UsersView)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        await reads.GetAsync(id, cancellationToken) is { } user ? Ok(user) : ControllerProblems.NotFound();

    /// <summary>Disables the user from their very next request. Idempotent.</summary>
    [HttpPost("{id:int}/disable")]
    [RequirePermission(PermissionCatalog.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disable(int id, CancellationToken cancellationToken) =>
        NoContentOr(await admin.ChangeStatusAsync(id, UserStatus.Disabled, cancellationToken));

    /// <summary>Re-enables a disabled user. Idempotent.</summary>
    [HttpPost("{id:int}/enable")]
    [RequirePermission(PermissionCatalog.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(int id, CancellationToken cancellationToken) =>
        NoContentOr(await admin.ChangeStatusAsync(id, UserStatus.Active, cancellationToken));

    /// <summary>Sets the user's roles to exactly <c>roleIds</c> (same user type only). Returns the updated user.</summary>
    [HttpPut("{id:int}/roles")]
    [RequirePermission(PermissionCatalog.UsersManage)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetRoles(int id, [FromBody] SetUserRolesRequest? request, CancellationToken cancellationToken) =>
        await UserOr(id, await admin.SetRolesAsync(new SetUserRolesCommand(id, request?.RoleIds), cancellationToken), cancellationToken);

    /// <summary>Sets an External user's supplier links to exactly <c>supplierRecordIds</c>. Returns the updated user.</summary>
    [HttpPut("{id:int}/suppliers")]
    [RequirePermission(PermissionCatalog.UsersManage)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetSuppliers(int id, [FromBody] SetUserSuppliersRequest? request, CancellationToken cancellationToken) =>
        await UserOr(id, await admin.SetSuppliersAsync(new SetUserSuppliersCommand(id, request?.SupplierRecordIds), cancellationToken), cancellationToken);

    private IActionResult NoContentOr(UserAdminOutcome outcome) =>
        outcome == UserAdminOutcome.Success ? NoContent() : Problem(outcome);

    private async Task<IActionResult> UserOr(int id, UserAdminOutcome outcome, CancellationToken cancellationToken) =>
        outcome == UserAdminOutcome.Success && await reads.GetAsync(id, cancellationToken) is { } user ? Ok(user) : Problem(outcome);

    private static IActionResult Problem(UserAdminOutcome outcome) => outcome switch
    {
        UserAdminOutcome.InvalidRequest => InvalidRequest("A list of positive ids is required (an empty list removes them all)."),
        UserAdminOutcome.NotFound or UserAdminOutcome.Success => ControllerProblems.NotFound(),
        UserAdminOutcome.CannotDisableSelf => ProblemResults.Create(
            StatusCodes.Status409Conflict, "Cannot disable yourself",
            "Another administrator must disable your account.", "cannot_disable_self").ToActionResult(),
        UserAdminOutcome.LastSiteAdmin => ProblemResults.Create(
            StatusCodes.Status409Conflict, "Last Site Admin",
            "This would leave no active Site Admin. Assign or enable another first.", "last_site_admin").ToActionResult(),
        UserAdminOutcome.RoleNotFound => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "Role not found", "One of the role ids doesn't exist.", "role_not_found").ToActionResult(),
        UserAdminOutcome.RoleTypeMismatch => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "Role type mismatch",
            "Internal users hold internal roles and External users external roles.", "role_type_mismatch").ToActionResult(),
        UserAdminOutcome.SupplierLinksExternalOnly => ProblemResults.Create(
            StatusCodes.Status400BadRequest, "External users only",
            "Supplier links are for External users; Internal users see every supplier.", "supplier_links_external_only").ToActionResult(),
        _ => throw new InvalidOperationException($"Unhandled {nameof(UserAdminOutcome)}: {outcome}."),
    };

    private static IActionResult InvalidRequest(string detail) =>
        ProblemResults.Create(StatusCodes.Status400BadRequest, "Invalid user request", detail, "invalid_request").ToActionResult();
}
