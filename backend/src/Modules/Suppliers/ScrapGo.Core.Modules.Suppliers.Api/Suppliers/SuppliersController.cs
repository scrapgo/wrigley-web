using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Suppliers (Quickbase table <c>bqrcgnatz</c>) for an organization's
/// application, e.g. Downstream. Read-only for now.
/// </summary>
/// <remarks>
/// The route carries <c>{organizationId}</c> and <c>{applicationId}</c>, so the
/// membership guard runs first: 403 without an active membership, 404
/// <c>application_not_found</c> if the organization doesn't have the
/// application. <see cref="SupplierService"/> then requires
/// <c>Downstream.Suppliers.Read</c> there, which also needs the Suppliers
/// module enabled.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:int}/applications/{applicationId:int}/suppliers")]
[Produces("application/json")]
public sealed class SuppliersController(SupplierService suppliers) : ControllerBase
{
    /// <summary>
    /// Suppliers with a name, sorted by name, one page at a time. <c>search</c>
    /// filters by a name substring; <c>top</c> is 1 to 1000 (default 100).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<SupplierListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> List(
        int organizationId,
        int applicationId,
        [FromQuery] string? search,
        [FromQuery] int? skip,
        [FromQuery] int? top,
        CancellationToken cancellationToken)
    {
        var result = await suppliers.ListAsync(
            new ListSuppliersQuery(SupplierScope.ForApplication(organizationId, applicationId), search, skip, top), cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Page: { } page }
            ? Ok(page)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }

    /// <summary>One supplier by its Quickbase Record ID#.</summary>
    [HttpGet("{recordId:int}")]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(int organizationId, int applicationId, int recordId, CancellationToken cancellationToken)
    {
        var result = await suppliers.GetAsync(SupplierScope.ForApplication(organizationId, applicationId), recordId, cancellationToken);

        return result is { Outcome: SupplierOutcome.Success, Supplier: { } supplier }
            ? Ok(supplier)
            : SupplierProblems.For(result.Outcome, SupplierPermissions.Read);
    }
}

/// <summary>ProblemDetails for supplier outcomes, shared by both supplier controllers.</summary>
internal static class SupplierProblems
{
    public static IActionResult For(SupplierOutcome outcome, string requiredPermission) => outcome switch
    {
        SupplierOutcome.Forbidden => ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Missing permission",
            requiredPermission == SupplierPermissions.Read
                ? $"Requires {SupplierPermissions.Read} for this organization's application, with its Suppliers module enabled."
                : $"Requires {SupplierPermissions.PlatformRead} at platform scope (a platform administrator signed in with Google Workspace).",
            "missing_permission").ToActionResult(),
        SupplierOutcome.NotFound => ProblemResults.Create(
            StatusCodes.Status404NotFound,
            "Supplier not found",
            "No supplier with that record id.",
            "supplier_not_found").ToActionResult(),
        SupplierOutcome.InvalidRequest => ProblemResults.Create(
            StatusCodes.Status400BadRequest,
            "Invalid supplier list request",
            $"skip must be 0 or more and top 1 to {SupplierService.MaxTop}.",
            "invalid_request").ToActionResult(),
        SupplierOutcome.SourceUnavailable => ProblemResults.Create(
            StatusCodes.Status502BadGateway,
            "Quickbase unavailable",
            "Quickbase didn't answer and no cached copy was available. Try again shortly.",
            "quickbase_unavailable").ToActionResult(),
        _ => throw new InvalidOperationException($"Unhandled {nameof(SupplierOutcome)}: {outcome}."),
    };
}
