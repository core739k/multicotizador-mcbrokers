using McBrokers.Domain.Quotations;

namespace McBrokers.Domain.Tests.Quotations;

public class ValuationTypeExtensionsTests
{
    [Theory]
    [InlineData(ValuationType.Agreed, true)]
    [InlineData(ValuationType.AgreedPlus10, true)]
    [InlineData(ValuationType.Invoice, true)]
    [InlineData(ValuationType.Commercial, false)]
    [InlineData(ValuationType.CommercialPlus10, false)]
    public void ShouldSendSumInsured_returns_true_only_for_user_specified_values(
        ValuationType valuation, bool expected)
    {
        valuation.ShouldSendSumInsured().Should().Be(expected);
    }
}
