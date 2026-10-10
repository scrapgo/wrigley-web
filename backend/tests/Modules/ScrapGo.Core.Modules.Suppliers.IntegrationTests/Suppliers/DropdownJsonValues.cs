// Every supplier dropdown is sent and accepted as its exact text, the same as its label.
using System.Text.Json;
using ScrapGo.Core.Modules.Suppliers.Application.Suppliers;

namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Suppliers;

public class DropdownJsonValues
{
    [Fact]
    public void Payment_terms_serialize_as_their_exact_text_and_round_trip() => AssertExactText<PaymentTerms>(PaymentTermsCatalog.Label);

    [Fact]
    public void Dead_freight_serializes_as_its_exact_text_and_round_trips() => AssertExactText<DeadFreight>(DeadFreightCatalog.Label);

    [Fact]
    public void Last_call_results_serialize_as_their_exact_text_and_round_trip() => AssertExactText<LastCallResult>(LastCallResults.Catalog.Label);

    [Fact]
    public void Supplier_objections_serialize_as_their_exact_text_and_round_trip() => AssertExactText<SupplierObjection>(SupplierObjections.Catalog.Label);

    private static void AssertExactText<TEnum>(Func<TEnum, string> label)
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var json = JsonSerializer.Serialize(value);
            Assert.Equal(JsonSerializer.Serialize(label(value)), json);
            Assert.Equal(value, JsonSerializer.Deserialize<TEnum>(json));
        }
    }
}
