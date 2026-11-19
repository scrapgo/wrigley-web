using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

/// <summary>
/// Where a load's supplier payment stands, derived by
/// <see cref="SupplierPaymentService.ClassifyPayment"/> (ITS Dispatch records no actual payments).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus
{
    /// <summary>Counted load whose Payment Date (572) is today or earlier.</summary>
    Paid,

    /// <summary>Counted load whose Payment Date (572) is after today.</summary>
    Scheduled,

    /// <summary>Counted load with no Payment Date (572).</summary>
    Unscheduled,

    /// <summary>The load's status isn't counted (e.g. Pending), so it is never shown as paid.</summary>
    NotApplicable,
}
