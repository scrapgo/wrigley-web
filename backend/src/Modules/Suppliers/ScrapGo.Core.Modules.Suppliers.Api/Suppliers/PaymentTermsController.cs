using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Suppliers.Api.Suppliers;

/// <summary>
/// Reference data for the supplier forms. Static and not Quickbase data, so
/// any signed-in user may read it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/suppliers/payment-terms")]
[Produces("application/json")]
public sealed class PaymentTermsController : ControllerBase
{
    /// <summary>
    /// The payment terms dropdown options, in order: <c>value</c> is what
    /// supplier responses carry (e.g. <c>"Net5"</c>), <c>label</c> what to show
    /// (e.g. <c>"Net 5"</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentTermsOption>>(StatusCodes.Status200OK)]
    public IActionResult List() => Ok(PaymentTermsCatalog.Options);
}
