using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Gnp.Mapping;

namespace McBrokers.Insurers.Gnp.Tests.Mapping;

public class GnpResponseParserTests
{
    private static string LoadRecorded(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", name));

    [Fact]
    public void Parses_successful_quote_response()
    {
        var rawResponse = LoadRecorded("quote_success.xml");

        var outcome = GnpResponseParser.Parse(rawRequest: "<X/>", rawResponse: rawResponse, latencyMs: 1234);

        var success = outcome.Should().BeOfType<InsurerQuoteOutcome.Success>().Subject;
        success.Response.PremiumTotal.Should().Be(11638.92m);
        success.Response.PremiumNet.Should().Be(9493.54m);
        success.Response.Tax.Should().Be(1605.38m);
        success.Response.Fees.Should().Be(540.00m);
        success.Response.ExternalQuoteRef.Should().Be("CIANNE231023021110");
        success.Response.LatencyMs.Should().Be(1234);
    }

    [Fact]
    public void Parses_error_response_0288_as_Business_failure()
    {
        var rawResponse = LoadRecorded("quote_error_0288.xml");

        var outcome = GnpResponseParser.Parse(rawRequest: "<X/>", rawResponse: rawResponse, latencyMs: 1234);

        var failure = outcome.Should().BeOfType<InsurerQuoteOutcome.Failure>().Subject;
        failure.Error.Status.Should().Be(QuotationInsurerStatus.Failed);
        failure.Error.Category.Should().Be(ErrorCategory.Business);
        failure.Error.ExternalCode.Should().Be("0288");
        failure.Error.ExternalMessage.Should().Contain("vehículo");
    }

    [Fact]
    public void Treats_malformed_xml_as_Technical_failure()
    {
        var outcome = GnpResponseParser.Parse(rawRequest: "<X/>", rawResponse: "<<<not xml>>>", latencyMs: 50);

        var failure = outcome.Should().BeOfType<InsurerQuoteOutcome.Failure>().Subject;
        failure.Error.Category.Should().Be(ErrorCategory.Technical);
        failure.Error.ExternalCode.Should().Be("PARSE_ERROR");
    }

    [Fact]
    public void Reports_missing_amounts_as_Technical_failure()
    {
        var xml = "<COTIZACION><SOLICITUD><NUM_COTIZACION>X</NUM_COTIZACION></SOLICITUD></COTIZACION>";

        var outcome = GnpResponseParser.Parse(rawRequest: "<X/>", rawResponse: xml, latencyMs: 10);

        var failure = outcome.Should().BeOfType<InsurerQuoteOutcome.Failure>().Subject;
        failure.Error.ExternalCode.Should().Be("MISSING_AMOUNTS");
    }
}
