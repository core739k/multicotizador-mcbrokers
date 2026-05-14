using McBrokers.Application.Quotations;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Tests.Quotations;

public class StatusLabelsTests
{
    [Theory]
    [InlineData(QuotationStatus.Pending, "En proceso")]
    [InlineData(QuotationStatus.Partial, "Parcial")]
    [InlineData(QuotationStatus.Completed, "Completada")]
    [InlineData(QuotationStatus.Failed, "No disponible")]
    public void Spanish_QuotationStatus_maps_each_enum_value(QuotationStatus status, string expected)
    {
        StatusLabels.Spanish(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(QuotationInsurerStatus.Pending, "En proceso")]
    [InlineData(QuotationInsurerStatus.Succeeded, "Cotización obtenida")]
    [InlineData(QuotationInsurerStatus.Failed, "No disponible")]
    [InlineData(QuotationInsurerStatus.Timeout, "Sin respuesta")]
    [InlineData(QuotationInsurerStatus.InsurerDown, "Sin respuesta")]
    [InlineData(QuotationInsurerStatus.NotCovered, "No disponible")]
    public void Spanish_QuotationInsurerStatus_maps_each_enum_value(QuotationInsurerStatus status, string expected)
    {
        StatusLabels.Spanish(status).Should().Be(expected);
    }
}
