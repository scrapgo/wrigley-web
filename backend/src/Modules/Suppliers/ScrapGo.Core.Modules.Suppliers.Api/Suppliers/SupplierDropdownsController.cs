using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Dropdown options for the call and prospect status form. Static and not
/// Quickbase data, so any signed-in user may read them. Each option's
/// <c>value</c> is what supplier responses carry; <c>label</c> is what to show.
/// </summary>
[ApiController]
[Authorize]
[Route("api/suppliers")]
[Produces("application/json")]
public sealed class SupplierDropdownsController : ControllerBase
{
    /// <summary>Last call result options, in order.</summary>
    [HttpGet("last-call-results")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<LastCallResult>>>(StatusCodes.Status200OK)]
    public IActionResult LastCallResultOptions() => Ok(LastCallResults.Catalog.Options);

    /// <summary>Supplier objection options, in order.</summary>
    [HttpGet("supplier-objections")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<SupplierObjection>>>(StatusCodes.Status200OK)]
    public IActionResult SupplierObjectionOptions() => Ok(SupplierObjections.Catalog.Options);
}
