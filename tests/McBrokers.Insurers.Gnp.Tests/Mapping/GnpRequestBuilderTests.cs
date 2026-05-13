using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Gnp.Mapping;

namespace McBrokers.Insurers.Gnp.Tests.Mapping;

public class GnpRequestBuilderTests
{
    private static InsurerQuoteRequest SampleRequest(PackageCode package = PackageCode.Amplia) => new(
        CorrelationId: "corr-abc-123",
        Credentials: new InsurerCredentials("ECONTR298814", "Mcbrokers040923", "NOP0000077"),
        Connection: new InsurerConnectionConfig("https://api.service.gnp.com.mx/autos/wsp/cotizador/cotizar", 30, 3),
        Vehicle: new VehicleSelection(
            Year: 2025, Brand: "CHEVROLET", Model: "AVEO", Version: "LT",
            ExternalClave: "GNP112233"),
        Package: package,
        PackageExternalCode: "CPAU0000123",
        PaymentMode: PaymentMode.Annual,
        ValuationType: ValuationType.Commercial,
        SumInsured: 250000m,
        Deductibles: new DeductiblesAndSums(5m, 10m, 200000m, 3000000m),
        Contractor: new ContactInfo(
            "Esteban", "Contreras", "Pérez",
            "06700", Gender.Male,
            new DateOnly(1990, 1, 15)),
        HabitualDriver: new DriverInfo("06700", Gender.Male, new DateOnly(1990, 1, 15)),
        PostalCode: "06700");

    [Fact]
    public void Builds_root_element_COTIZACION()
    {
        var xml = GnpRequestBuilder.BuildQuoteRequest(SampleRequest(), new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Root!.Name.LocalName.Should().Be("COTIZACION");
    }

    [Fact]
    public void Embeds_credentials_in_body()
    {
        var xml = GnpRequestBuilder.BuildQuoteRequest(SampleRequest(), new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Descendants("USUARIO").Single().Value.Should().Be("ECONTR298814");
        doc.Descendants("PASSWORD").Single().Value.Should().Be("Mcbrokers040923");
        doc.Descendants("ID_UNIDAD_OPERABLE").Single().Value.Should().Be("NOP0000077");
    }

    [Fact]
    public void Sets_vigencia_one_year_apart()
    {
        var xml = GnpRequestBuilder.BuildQuoteRequest(SampleRequest(), new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Descendants("FCH_INICIO_VIGENCIA").Single().Value.Should().Be("20260511");
        doc.Descendants("FCH_FIN_VIGENCIA").Single().Value.Should().Be("20270511");
    }

    [Fact]
    public void Decodes_amis_clave_positionally()
    {
        var (armadora, carroceria, version) = GnpRequestBuilder.DecodeAmisClave("GNP112233");

        armadora.Should().Be("11");
        carroceria.Should().Be("22");
        version.Should().Be("33");
    }

    [Theory]
    [InlineData(PaymentMode.Annual, "A")]
    [InlineData(PaymentMode.Semestral, "S")]
    [InlineData(PaymentMode.Trimestral, "T")]
    [InlineData(PaymentMode.Monthly, "M")]
    [InlineData(PaymentMode.Dxn, "A")]      // DXN (descuento por nómina) viaja como Annual
    public void Maps_payment_periodicity(PaymentMode mode, string expected)
    {
        GnpRequestBuilder.MapPeriodicity(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ValuationType.Commercial, "0")]
    [InlineData(ValuationType.CommercialPlus10, "0")]
    [InlineData(ValuationType.Agreed, "250000")]
    [InlineData(ValuationType.AgreedPlus10, "250000")]
    [InlineData(ValuationType.Invoice, "250000")]
    public void VALOR_FACTURA_sent_only_when_valuation_type_requires_it(ValuationType valuation, string expected)
    {
        var req = SampleRequest() with { ValuationType = valuation, SumInsured = 250000m };
        var xml = GnpRequestBuilder.BuildQuoteRequest(req, new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Descendants("VALOR_FACTURA").Single().Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(ValuationType.Commercial, "01")]
    [InlineData(ValuationType.CommercialPlus10, "08")]
    [InlineData(ValuationType.Agreed, "03")]
    [InlineData(ValuationType.AgreedPlus10, "04")]
    [InlineData(ValuationType.Invoice, "02")]
    public void Maps_valuation_type(ValuationType v, string expected)
    {
        GnpRequestBuilder.MapValuation(v).Should().Be(expected);
    }

    [Fact]
    public void Removes_accents_from_contractor_name()
    {
        var req = SampleRequest() with
        {
            Contractor = new ContactInfo("José", "García", "Pérez", "06700", Gender.Male, new DateOnly(1990, 1, 15)),
        };

        var xml = GnpRequestBuilder.BuildQuoteRequest(req, new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Descendants("NOMBRE").First(e => e.Value is "Jose").Value.Should().Be("Jose");
        doc.Descendants("APELLIDO_PATERNO").Single().Value.Should().Be("Garcia");
        doc.Descendants("APELLIDO_MATERNO").Single().Value.Should().Be("Perez");
    }

    [Fact]
    public void Includes_DM_coverages_only_for_AMPLIA()
    {
        var amplia = XDocument.Parse(GnpRequestBuilder.BuildQuoteRequest(
            SampleRequest(PackageCode.Amplia), new DateOnly(2026, 5, 11)));
        var limitada = XDocument.Parse(GnpRequestBuilder.BuildQuoteRequest(
            SampleRequest(PackageCode.Limitada), new DateOnly(2026, 5, 11)));
        var rc = XDocument.Parse(GnpRequestBuilder.BuildQuoteRequest(
            SampleRequest(PackageCode.ResponsabilidadCivil), new DateOnly(2026, 5, 11)));

        AllCoverageCodes(amplia).Should().Contain("0000001288").And.Contain("0000001289").And.Contain("0000000916");
        AllCoverageCodes(limitada).Should().Contain("0000000916").And.NotContain("0000001288").And.NotContain("0000001289");
        AllCoverageCodes(rc).Should().NotContain("0000001288").And.NotContain("0000000916");
    }

    [Fact]
    public void RC_package_only_carries_third_party_coverages()
    {
        var rc = XDocument.Parse(GnpRequestBuilder.BuildQuoteRequest(
            SampleRequest(PackageCode.ResponsabilidadCivil), new DateOnly(2026, 5, 11)));

        AllCoverageCodes(rc).Should().BeEquivalentTo(
            new[] { "0000000906", "0000001273", "0000001285", "0000000904" });
    }

    [Fact]
    public void CVE_PAQUETE_uses_PackageExternalCode_from_request()
    {
        var xml = GnpRequestBuilder.BuildQuoteRequest(SampleRequest(), new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Descendants("CVE_PAQUETE").Single().Value.Should().Be("CPAU0000123");
    }

    [Fact]
    public void Computes_driver_age_correctly()
    {
        GnpRequestBuilder.AgeOn(new DateOnly(2026, 5, 11), new DateOnly(1990, 5, 11)).Should().Be(36);
        GnpRequestBuilder.AgeOn(new DateOnly(2026, 5, 11), new DateOnly(1990, 5, 12)).Should().Be(35);
        GnpRequestBuilder.AgeOn(new DateOnly(2026, 5, 11), new DateOnly(2026, 5, 11)).Should().Be(0);
    }

    private static IEnumerable<string> AllCoverageCodes(XDocument doc) =>
        doc.Descendants("CVE_COBERTURA").Select(e => e.Value);
}
