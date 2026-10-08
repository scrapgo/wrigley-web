using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Reference data for the supplier forms. Static and not Quickbase data, so
/// any signed-in user may read it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/suppliers/dead-freight")]
[Produces("application/json")]
public sealed class DeadFreightController : ControllerBase
{
    /// <summary>
    /// The dead freight dropdown options, in order: <c>value</c> is what
    /// supplier responses carry (<c>"Exempt"</c>, <c>"NotExempt"</c>),
    /// <c>label</c> what to show (<c>"Exempt"</c>, <c>"Not Exempt"</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeadFreightOption>>(StatusCodes.Status200OK)]
    public IActionResult List() => Ok(DeadFreightCatalog.Options);
}
