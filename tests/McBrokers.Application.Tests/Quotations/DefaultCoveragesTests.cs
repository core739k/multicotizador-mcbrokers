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
    public void Available_sets_default_to_empty_so_appsettings_is_authoritative()
    {
        var sut = new DefaultCoverages();

        // El binder de Microsoft.Extensions.Configuration concatena arrays
        // JSON con arrays existentes en el POCO en lugar de reemplazarlos.
        // Si defaults POCO = [5,10,15,20] y appsettings también = [5,10,15,20],
        // el resultado es [5,10,15,20,5,10,15,20] — los dropdowns mostraban
        // las opciones duplicadas. Defaults vacíos eliminan ese bug.
        sut.AvailableDMPct.Should().BeEmpty();
        sut.AvailableRTPct.Should().BeEmpty();
        sut.AvailableGMO.Should().BeEmpty();
    }

    [Fact]
    public void Binding_does_not_duplicate_arrays_from_appsettings()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cotizacion:DefaultCoverages:AvailableDMPct:0"] = "5",
            ["Cotizacion:DefaultCoverages:AvailableDMPct:1"] = "10",
            ["Cotizacion:DefaultCoverages:AvailableDMPct:2"] = "15",
            ["Cotizacion:DefaultCoverages:AvailableDMPct:3"] = "20",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var bound = config.GetSection(DefaultCoverages.ConfigSection).Get<DefaultCoverages>();

        bound!.AvailableDMPct.Should().Equal(5m, 10m, 15m, 20m);
        bound.AvailableDMPct.Should().HaveCount(4,
            because: "appsettings provides 4 values and POCO defaults are empty — no duplication");
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
