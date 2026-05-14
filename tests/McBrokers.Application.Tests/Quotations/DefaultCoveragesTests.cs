using McBrokers.Application.Quotations;
using Microsoft.Extensions.Configuration;

namespace McBrokers.Application.Tests.Quotations;

public class DefaultCoveragesTests
{
    [Fact]
    public void Defaults_match_the_legacy_hardcoded_values()
    {
        var sut = new DefaultCoverages();

        sut.MaterialDamagesDeductiblePct.Should().Be(5m);
        sut.RobberyDeductiblePct.Should().Be(10m);
        sut.MedicalExpensesSumInsured.Should().Be(200_000m);
        sut.CivilLiabilitySumInsured.Should().Be(3_000_000m);
    }

    [Fact]
    public void Available_sets_default_to_POC_lists()
    {
        var sut = new DefaultCoverages();

        sut.AvailableDMPct.Should().Equal(5m, 10m, 15m, 20m);
        sut.AvailableRTPct.Should().Equal(5m, 10m, 15m, 20m);
        sut.AvailableGMO.Should().Equal(50_000m, 100_000m, 200_000m, 300_000m, 500_000m);
    }

    [Fact]
    public void Binds_from_configuration_section()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cotizacion:DefaultCoverages:MaterialDamagesDeductiblePct"] = "7.5",
            ["Cotizacion:DefaultCoverages:RobberyDeductiblePct"] = "12",
            ["Cotizacion:DefaultCoverages:MedicalExpensesSumInsured"] = "300000",
            ["Cotizacion:DefaultCoverages:CivilLiabilitySumInsured"] = "5000000",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var bound = config.GetSection(DefaultCoverages.ConfigSection).Get<DefaultCoverages>();

        bound.Should().NotBeNull();
        bound!.MaterialDamagesDeductiblePct.Should().Be(7.5m);
        bound.RobberyDeductiblePct.Should().Be(12m);
        bound.MedicalExpensesSumInsured.Should().Be(300_000m);
        bound.CivilLiabilitySumInsured.Should().Be(5_000_000m);
    }

    [Fact]
    public void Partial_override_keeps_defaults_for_unset_keys()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cotizacion:DefaultCoverages:CivilLiabilitySumInsured"] = "10000000",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var bound = new DefaultCoverages();
        config.GetSection(DefaultCoverages.ConfigSection).Bind(bound);

        bound.CivilLiabilitySumInsured.Should().Be(10_000_000m);
        bound.MaterialDamagesDeductiblePct.Should().Be(5m, because: "unset keys preserve the POCO default");
    }
}
