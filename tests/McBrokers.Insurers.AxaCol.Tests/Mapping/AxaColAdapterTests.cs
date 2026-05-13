using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.AxaCol.Mapping;

namespace McBrokers.Insurers.AxaCol.Tests.Mapping;

public class AxaColAdapterTests
{
    private static InsurerQuoteRequest SampleRequest() => new(
        CorrelationId: "corr-ac-001",
        Credentials: new InsurerCredentials("AXAUSER", "AXAPASS", "AUTOS_PRODUCT"),
        Connection: new InsurerConnectionConfig(
            "https://serviciosweb.axa.com.mx:9104/EmisionPolizasWS/services/SolicitudPolizasService", 50, 3),
        Vehicle: new VehicleSelection(2025, "CHEVROLET", "AVEO", "LT", "01112233"),
        Package: PackageCode.Amplia,
        PackageExternalCode: "AUTOS",
        PaymentMode: PaymentMode.Annual,
        ValuationType: ValuationType.Commercial,
        SumInsured: 250000m,
        Deductibles: new DeductiblesAndSums(5m, 10m, 200000m, 3000000m),
        Contractor: new ContactInfo("Esteban", "Contreras", "Perez", "06700", Gender.Male, new DateOnly(1990, 1, 15)),
        HabitualDriver: new DriverInfo("06700", Gender.Male, new DateOnly(1990, 1, 15)),
        PostalCode: "06700");

    [Fact]
    public void Soap_envelope_embeds_solicitud_in_CDATA()
    {
        var inner = AxaColRequestBuilder.BuildSolicitudXml(SampleRequest(), new DateOnly(2026, 5, 11));
        var envelope = AxaColRequestBuilder.BuildSoapEnvelope(inner);

        envelope.Should().Contain("<![CDATA[");
        envelope.Should().Contain("]]>");
        envelope.Should().Contain("createSolicitudPolizasInmediata");
        envelope.Should().Contain("xsi:type=\"xsd:string\"");
    }

    [Fact]
    public void Solicitud_carries_TipoMovimiento_COTIZACION()
    {
        var inner = AxaColRequestBuilder.BuildSolicitudXml(SampleRequest(), new DateOnly(2026, 5, 11));
        inner.Should().Contain("<TipoMovimiento>COTIZACION</TipoMovimiento>");
        inner.Should().Contain("<TipoPoliza>COLECTIVA</TipoPoliza>");
    }

    [Theory]
    [InlineData(ValuationType.Commercial, "0")]
    [InlineData(ValuationType.CommercialPlus10, "0")]
    [InlineData(ValuationType.Agreed, "250000")]
    [InlineData(ValuationType.AgreedPlus10, "250000")]
    [InlineData(ValuationType.Invoice, "250000")]
    public void ValorComercial_sent_only_when_valuation_type_requires_it(ValuationType valuation, string expected)
    {
        var req = SampleRequest() with { ValuationType = valuation, SumInsured = 250000m };
        var xml = AxaColRequestBuilder.BuildSolicitudXml(req, new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        doc.Descendants("ValorComercial").Single().Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(PaymentMode.Annual, "CONTADO")]
    [InlineData(PaymentMode.Semestral, "SEMESTRAL")]
    [InlineData(PaymentMode.Trimestral, "TRIMESTRAL")]
    [InlineData(PaymentMode.Monthly, "MENSUAL")]
    [InlineData(PaymentMode.Dxn, "CONTADO")]   // DXN (descuento por nómina) viaja como Annual
    public void Maps_payment_mode(PaymentMode mode, string expected)
    {
        AxaColRequestBuilder.MapPaymentMode(mode).Should().Be(expected);
    }

    [Fact]
    public void Parses_successful_response()
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", "quote_success.xml"));

        var outcome = AxaColResponseParser.Parse("<X/>", raw, 2500);

        var s = outcome.Should().BeOfType<InsurerQuoteOutcome.Success>().Subject;
        s.Response.PremiumTotal.Should().Be(8612.00m);
        s.Response.PremiumNet.Should().Be(7200.00m);
        s.Response.Tax.Should().Be(1152.00m);
        s.Response.Fees.Should().Be(260.00m);
        s.Response.ExternalQuoteRef.Should().Be("AXACOL-202605110042");
    }
}
