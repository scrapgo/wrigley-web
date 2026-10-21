using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>ProblemDetails for supplier outcomes.</summary>
internal static class SupplierProblems
{
    public static IActionResult For(SupplierOutcome outcome) => outcome switch
    {
        SupplierOutcome.Forbidden => ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Missing permission",
            $"Requires {SupplierPermissions.ViewAll} or {SupplierPermissions.ViewOwn}.",
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
