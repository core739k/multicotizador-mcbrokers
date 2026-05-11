using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.AxaDxn.Mapping;

namespace McBrokers.Insurers.AxaDxn.Tests.Mapping;

public class AxaDxnAdapterTests
{
    private static InsurerQuoteRequest SampleRequest(PackageCode package = PackageCode.Amplia) => new(
        CorrelationId: "corr-ad-001",
        Credentials: new InsurerCredentials("MXS00102308A", "Id9LkOi30nceHCu4qh6F", "POL000123"),
        EnvironmentConfig: new InsurerEnvironmentConfig(
            "https://serviciosweb.axa.com.mx:9104/WSFlotillas/services/FlotillasService", 50, 3),
        Vehicle: new VehicleSelection(2025, "CHEVROLET", "AVEO", "LT", "01112233"),
        Package: package,
        PackageExternalCode: "AUTOS",
        PaymentMode: PaymentMode.Annual,
        ValuationType: ValuationType.Commercial,
        SumInsured: 250000m,
        Deductibles: new DeductiblesAndSums(5m, 10m, 200000m, 3000000m),
        Contractor: new ContactInfo("Esteban", "Contreras", "Perez", "06700", Gender.Male, new DateOnly(1990, 1, 15)),
        HabitualDriver: new DriverInfo("06700", Gender.Male, new DateOnly(1990, 1, 15)),
        PostalCode: "06700");

    [Fact]
    public void Soap_envelope_contains_CotizarIncisoRequest_with_flotillas_namespace()
    {
        var soap = AxaDxnRequestBuilder.BuildSoapEnvelope(SampleRequest(), new DateOnly(2026, 5, 11));

        soap.Should().Contain("CotizarIncisoRequest");
        soap.Should().Contain(AxaDxnRequestBuilder.FlotillasNamespace);
    }

    [Theory]
    [InlineData(ValuationType.Commercial, "Comercial", "100")]
    [InlineData(ValuationType.CommercialPlus10, "Comercial", "110")]
    [InlineData(ValuationType.Agreed, "Convenido", "100")]
    [InlineData(ValuationType.AgreedPlus10, "Convenido", "110")]
    [InlineData(ValuationType.Invoice, "Factura", "100")]
    public void Maps_valuation_descriptor_and_percentage(ValuationType v, string descriptor, string pct)
    {
        AxaDxnRequestBuilder.MapValuationDescriptor(v).Should().Be(descriptor);
        AxaDxnRequestBuilder.MapValuationPercentage(v).Should().Be(pct);
    }

    [Fact]
    public void Body_includes_DM_only_for_AMPLIA()
    {
        var amplia = AxaDxnRequestBuilder.BuildSoapEnvelope(SampleRequest(PackageCode.Amplia), new DateOnly(2026, 5, 11));
        var limitada = AxaDxnRequestBuilder.BuildSoapEnvelope(SampleRequest(PackageCode.Limitada), new DateOnly(2026, 5, 11));
        var rc = AxaDxnRequestBuilder.BuildSoapEnvelope(SampleRequest(PackageCode.ResponsabilidadCivil), new DateOnly(2026, 5, 11));

        amplia.Should().Contain("<claveCobertura>DM</claveCobertura>");
        amplia.Should().Contain("<claveCobertura>RT</claveCobertura>");
        limitada.Should().NotContain("<claveCobertura>DM</claveCobertura>");
        limitada.Should().Contain("<claveCobertura>RT</claveCobertura>");
        rc.Should().NotContain("<claveCobertura>DM</claveCobertura>");
        rc.Should().NotContain("<claveCobertura>RT</claveCobertura>");
        rc.Should().Contain("<claveCobertura>RC</claveCobertura>");
    }

    [Fact]
    public void Parses_successful_response()
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", "quote_success.xml"));

        var outcome = AxaDxnResponseParser.Parse("<X/>", raw, 3000);

        var s = outcome.Should().BeOfType<InsurerQuoteOutcome.Success>().Subject;
        s.Response.PremiumTotal.Should().Be(10896.58m);
        s.Response.PremiumNet.Should().Be(9100.50m);
        s.Response.Tax.Should().Be(1456.08m);
        s.Response.Fees.Should().Be(340.00m);
        s.Response.ExternalQuoteRef.Should().Be("AXADXN-202605110099");
    }
}
