using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Ana.Mapping;

namespace McBrokers.Insurers.Ana.Tests.Mapping;

public class AnaAdapterTests
{
    private static InsurerQuoteRequest SampleRequest(PackageCode package = PackageCode.Amplia) => new(
        CorrelationId: "corr-a-001",
        Credentials: new InsurerCredentials("13503", "9zbi10Ly", "2213"),
        Connection: new InsurerConnectionConfig("https://server.anaseguros.com.mx/WSCOR/service.asmx", 30, 3),
        Vehicle: new VehicleSelection(2025, "CHEVROLET", "AVEO", "LT", "12345AB"),
        Package: package,
        PackageExternalCode: "1",
        PaymentMode: PaymentMode.Annual,
        ValuationType: ValuationType.Commercial,
        SumInsured: 250000m,
        Deductibles: new DeductiblesAndSums(5m, 10m, 200000m, 3000000m),
        Contractor: new ContactInfo("Esteban", "Contreras", "Perez", "06700", Gender.Male, new DateOnly(1990, 1, 15)),
        HabitualDriver: new DriverInfo("06700", Gender.Male, new DateOnly(1990, 1, 15)),
        PostalCode: "09002");

    [Theory]
    [InlineData(PackageCode.Amplia, "1")]
    [InlineData(PackageCode.Limitada, "3")]
    [InlineData(PackageCode.ResponsabilidadCivil, "4")]
    public void Maps_plan_attribute(PackageCode pkg, string expected)
    {
        var xml = AnaRequestBuilder.BuildTransaccionesXml(SampleRequest(pkg), "09002");
        var doc = XDocument.Parse(xml);
        doc.Descendants("vehiculo").Single().Attribute("plan")!.Value.Should().Be(expected);
    }

    [Fact]
    public void Coverage_set_filters_by_package()
    {
        var amplia = XDocument.Parse(AnaRequestBuilder.BuildTransaccionesXml(SampleRequest(PackageCode.Amplia), "09002"));
        var limitada = XDocument.Parse(AnaRequestBuilder.BuildTransaccionesXml(SampleRequest(PackageCode.Limitada), "09002"));
        var rc = XDocument.Parse(AnaRequestBuilder.BuildTransaccionesXml(SampleRequest(PackageCode.ResponsabilidadCivil), "09002"));

        Coverages(amplia).Should().Contain("02").And.Contain("04");
        Coverages(limitada).Should().NotContain("02").And.Contain("04");
        Coverages(rc).Should().NotContain("02").And.NotContain("04");
        // Cobertura común en todos: 06, 07, 10, 25, 26, 34, 23
        Coverages(rc).Should().Contain("34").And.Contain("23");
    }

    [Fact]
    public void EdoMun_is_attribute_on_vehiculo_and_asegurado()
    {
        var doc = XDocument.Parse(AnaRequestBuilder.BuildTransaccionesXml(SampleRequest(), "09002"));

        doc.Descendants("vehiculo").Single().Attribute("estado")!.Value.Should().Be("09002");
        doc.Descendants("asegurado").Single().Attribute("estado")!.Value.Should().Be("09002");
    }

    [Theory]
    [InlineData(PaymentMode.Annual, "C")]
    [InlineData(PaymentMode.Semestral, "S")]
    [InlineData(PaymentMode.Trimestral, "T")]
    [InlineData(PaymentMode.Monthly, "M")]
    [InlineData(PaymentMode.Dxn, "C")]      // DXN (descuento por nómina) viaja como Annual
    public void Maps_payment_mode(PaymentMode mode, string expected)
    {
        AnaRequestBuilder.MapPaymentMode(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ValuationType.Commercial, "0")]
    [InlineData(ValuationType.CommercialPlus10, "0")]
    [InlineData(ValuationType.Agreed, "250000")]
    [InlineData(ValuationType.AgreedPlus10, "250000")]
    [InlineData(ValuationType.Invoice, "250000")]
    public void Coverage_02_sa_attribute_sent_only_when_valuation_type_requires_it(ValuationType valuation, string expected)
    {
        // cobertura id="02" (Daños Materiales) lleva sa con el SumInsured; presente solo en Amplia.
        var req = SampleRequest(PackageCode.Amplia) with { ValuationType = valuation, SumInsured = 250000m };
        var xml = AnaRequestBuilder.BuildTransaccionesXml(req, "09002");
        var doc = XDocument.Parse(xml);

        var dm = doc.Descendants("cobertura").Single(c => c.Attribute("id")!.Value == "02");
        dm.Attribute("sa")!.Value.Should().Be(expected);
    }

    [Fact]
    public void Parses_successful_response()
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", "quote_success.xml"));

        var outcome = AnaResponseParser.Parse("<X/>", raw, 1234);

        var s = outcome.Should().BeOfType<InsurerQuoteOutcome.Success>().Subject;
        s.Response.PremiumTotal.Should().Be(10160.00m);
        s.Response.PremiumNet.Should().Be(8500.00m);
        s.Response.Tax.Should().Be(1360.00m);
        s.Response.Fees.Should().Be(300.00m);
        s.Response.ExternalQuoteRef.Should().Be("ANA-202605110007");
    }

    private static IEnumerable<string> Coverages(XDocument doc) =>
        doc.Descendants("cobertura").Select(c => c.Attribute("id")!.Value);
}
