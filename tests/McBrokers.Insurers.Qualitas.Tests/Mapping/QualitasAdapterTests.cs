using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Qualitas.Mapping;

namespace McBrokers.Insurers.Qualitas.Tests.Mapping;

public class QualitasAdapterTests
{
    private static InsurerQuoteRequest SampleRequest(PackageCode package = PackageCode.Amplia) => new(
        CorrelationId: "corr-q-001",
        Credentials: new InsurerCredentials("75069", "LINEA", "07738"),
        Connection: new InsurerConnectionConfig("http://sio.qualitas.com.mx/WsEmision/WsEmision.asmx", 30, 3),
        Vehicle: new VehicleSelection(2025, "CHEVROLET", "AVEO", "LT", "12345AB"),
        Package: package,
        PackageExternalCode: "1",
        PaymentMode: PaymentMode.Annual,
        ValuationType: ValuationType.Commercial,
        SumInsured: 250000m,
        Deductibles: new DeductiblesAndSums(5m, 10m, 200000m, 3000000m),
        Contractor: new ContactInfo("Esteban", "Contreras", "Perez", "06700", Gender.Male, new DateOnly(1990, 1, 15)),
        HabitualDriver: new DriverInfo("06700", Gender.Male, new DateOnly(1990, 1, 15)),
        PostalCode: "06700");

    [Fact]
    public void Movimientos_carries_TipoMovimiento_2_and_business_unit()
    {
        var xml = QualitasRequestBuilder.BuildMovimientosXml(SampleRequest(), new DateOnly(2026, 5, 11));
        var doc = XDocument.Parse(xml);

        var mov = doc.Descendants("Movimiento").Single();
        mov.Attribute("TipoMovimiento")!.Value.Should().Be("2");
        mov.Attribute("NoNegocio")!.Value.Should().Be("07738");
    }

    [Fact]
    public void Includes_DM_coverage_only_for_AMPLIA()
    {
        var amplia = XDocument.Parse(QualitasRequestBuilder.BuildMovimientosXml(SampleRequest(PackageCode.Amplia), new DateOnly(2026, 5, 11)));
        var limitada = XDocument.Parse(QualitasRequestBuilder.BuildMovimientosXml(SampleRequest(PackageCode.Limitada), new DateOnly(2026, 5, 11)));
        var rc = XDocument.Parse(QualitasRequestBuilder.BuildMovimientosXml(SampleRequest(PackageCode.ResponsabilidadCivil), new DateOnly(2026, 5, 11)));

        Coberturas(amplia).Should().Contain("01").And.Contain("03");
        Coberturas(limitada).Should().Contain("03").And.NotContain("01");
        Coberturas(rc).Should().NotContain("01").And.NotContain("03");
    }

    [Theory]
    [InlineData(PaymentMode.Annual, "C")]
    [InlineData(PaymentMode.Semestral, "S")]
    [InlineData(PaymentMode.Trimestral, "T")]
    [InlineData(PaymentMode.Monthly, "M")]
    public void Maps_payment_mode(PaymentMode mode, string expected)
    {
        QualitasRequestBuilder.MapPaymentMode(mode).Should().Be(expected);
    }

    [Fact]
    public void AMIS_verifier_completes_sum_to_multiple_of_10()
    {
        // Example from the docs: 12345 → sum = (1+3+5)*3 + (2+4) = 27 + 6 = 33 → digit 7 (33+7=40).
        QualitasRequestBuilder.CalculateAmisVerifier("12345").Should().Be(7);
        // 00000 → already multiple of 10.
        QualitasRequestBuilder.CalculateAmisVerifier("00000").Should().Be(0);
    }

    [Fact]
    public void Soap_envelope_wraps_movimientos_inside_obtenerNuevaEmision()
    {
        var inner = QualitasRequestBuilder.BuildMovimientosXml(SampleRequest(), new DateOnly(2026, 5, 11));
        var envelope = QualitasRequestBuilder.BuildSoapBody(inner);

        envelope.Should().Contain("soap:Envelope");
        envelope.Should().Contain("obtenerNuevaEmision");
        envelope.Should().Contain("XmlCotiza");
    }

    [Fact]
    public void Parses_successful_response()
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", "quote_success.xml"));

        var outcome = QualitasResponseParser.Parse("<X/>", raw, 1234);

        var s = outcome.Should().BeOfType<InsurerQuoteOutcome.Success>().Subject;
        s.Response.PremiumTotal.Should().Be(11638.92m);
        s.Response.PremiumNet.Should().Be(9493.54m);
        s.Response.Tax.Should().Be(1605.38m);
        s.Response.Fees.Should().Be(540.00m);
        s.Response.ExternalQuoteRef.Should().Be("QUA-202605110001");
    }

    [Fact]
    public void Parses_0288_as_Business_failure()
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", "quote_error_0288.xml"));

        var outcome = QualitasResponseParser.Parse("<X/>", raw, 1234);

        var f = outcome.Should().BeOfType<InsurerQuoteOutcome.Failure>().Subject;
        f.Error.Category.Should().Be(ErrorCategory.Business);
        f.Error.ExternalCode.Should().StartWith("0288");
    }

    private static IEnumerable<string> Coberturas(XDocument doc) =>
        doc.Descendants("Coberturas").Select(c => c.Attribute("NoCobertura")!.Value);
}
