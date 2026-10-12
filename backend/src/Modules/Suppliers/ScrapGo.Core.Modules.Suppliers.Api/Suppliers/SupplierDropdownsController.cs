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

    /// <summary>In-stock item material type options, in order.</summary>
    [HttpGet("in-stock-items/material-types")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<MaterialType>>>(StatusCodes.Status200OK)]
    public IActionResult MaterialTypeOptions() => Ok(InStockItemDropdowns.MaterialTypes.Options);

    /// <summary>In-stock item unit of measure options, in order.</summary>
    [HttpGet("in-stock-items/units-of-measure")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<UnitOfMeasure>>>(StatusCodes.Status200OK)]
    public IActionResult UnitOfMeasureOptions() => Ok(InStockItemDropdowns.UnitsOfMeasure.Options);

    /// <summary>In-stock item preparation options, in order.</summary>
    [HttpGet("in-stock-items/preparations")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<Preparation>>>(StatusCodes.Status200OK)]
    public IActionResult PreparationOptions() => Ok(InStockItemDropdowns.Preparations.Options);

    /// <summary>In-stock item availability options, in order.</summary>
    [HttpGet("in-stock-items/availabilities")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<Availability>>>(StatusCodes.Status200OK)]
    public IActionResult AvailabilityOptions() => Ok(InStockItemDropdowns.Availabilities.Options);

    /// <summary>In-stock item truck type options, in order.</summary>
    [HttpGet("in-stock-items/truck-types")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<TruckType>>>(StatusCodes.Status200OK)]
    public IActionResult TruckTypeOptions() => Ok(InStockItemDropdowns.TruckTypes.Options);

    /// <summary>In-stock item dirty or clean options, in order.</summary>
    [HttpGet("in-stock-items/dirty-or-clean")]
    [ProducesResponseType<IReadOnlyList<DropdownOption<DirtyOrClean>>>(StatusCodes.Status200OK)]
    public IActionResult DirtyOrCleanOptions() => Ok(InStockItemDropdowns.DirtyOrClean.Options);
}
