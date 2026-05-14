using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.AxaDxn.Mapping;

namespace McBrokers.Insurers.AxaDxn.Tests.Mapping;

/// <summary>
/// COPSIS devuelve SOAP+CDATA con XML interno:
///   /soap:Envelope/soap:Body/tempuri:EmiteAxaResponse/tempuri:EmiteAxaResult
///     CDATA → &lt;RESPUESTA&gt;...&lt;/RESPUESTA&gt;
///
/// El legacy detecta error si el CDATA contiene "error"; en éxito lee
/// &lt;url&gt; (PDF poliza) e &lt;inciso&gt; (folio). Reescritura del parser
/// (era JSON-first y nunca podía funcionar contra el endpoint real).
/// </summary>
public class AxaDxnCopsisEmitResponseParserTests
{
    private static string LoadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedResponses", fileName));

    [Fact]
    public void Success_parses_url_and_inciso_from_CDATA_RESPUESTA()
    {
        var raw = LoadFixture("emit_success.xml");

        var outcome = AxaDxnCopsisEmitResponseParser.Parse("req-body", raw, latencyMs: 322);

        var ok = outcome.Should().BeOfType<InsurerEmitOutcome.Success>().Subject;
        ok.Response.PolicyNumber.Should().Be("00012345");
        ok.Response.PdfDownloadUrl.Should().Be("https://lb1.copsis.com/poliza/AXA-2026-00012345.pdf");
        ok.Response.LatencyMs.Should().Be(322);
        ok.Response.RawRequest.Should().Be("req-body");
        ok.Response.RawResponse.Should().Be(raw);
    }

    [Fact]
    public void Error_when_CDATA_contains_word_error()
    {
        var raw = LoadFixture("emit_error.xml");

        var outcome = AxaDxnCopsisEmitResponseParser.Parse("req-body", raw, latencyMs: 280);

        var fail = outcome.Should().BeOfType<InsurerEmitOutcome.Failure>().Subject;
        fail.Error.Category.Should().Be(ErrorCategory.Business);
        fail.Error.ExternalCode.Should().Be("COPSIS_ERROR");
        fail.Error.ExternalMessage.Should().Contain("Cotización no encontrada");
        fail.Error.RawResponse.Should().Be(raw);
    }

    [Fact]
    public void Dummy_template_without_url_or_inciso_is_treated_as_missing_data()
    {
        // El template <RESPUESTA>response_type</RESPUESTA> es lo que COPSIS devuelve
        // cuando las llaves d4_key/b son inválidas — no contiene "error" en el CDATA,
        // pero tampoco tiene <url> ni <inciso>. Sin esos campos, no podemos avanzar.
        var raw = LoadFixture("emit_template_dummy.xml");

        var outcome = AxaDxnCopsisEmitResponseParser.Parse("req-body", raw, latencyMs: 110);

        var fail = outcome.Should().BeOfType<InsurerEmitOutcome.Failure>().Subject;
        fail.Error.ExternalCode.Should().Be("MISSING_POLIZA");
        fail.Error.Category.Should().Be(ErrorCategory.Technical);
    }

    [Fact]
    public void Malformed_outer_SOAP_returns_PARSE_ERROR()
    {
        var raw = "<not-xml at all";

        var outcome = AxaDxnCopsisEmitResponseParser.Parse("req-body", raw, latencyMs: 50);

        var fail = outcome.Should().BeOfType<InsurerEmitOutcome.Failure>().Subject;
        fail.Error.ExternalCode.Should().Be("PARSE_ERROR");
        fail.Error.Category.Should().Be(ErrorCategory.Technical);
    }

    [Fact]
    public void SOAP_without_EmiteAxaResult_node_returns_PARSE_ERROR()
    {
        // SOAP válido pero sin el nodo esperado — no podemos extraer CDATA.
        var raw = """
        <?xml version="1.0"?>
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <Otra xmlns="http://tempuri.org/">algo</Otra>
          </soap:Body>
        </soap:Envelope>
        """;

        var outcome = AxaDxnCopsisEmitResponseParser.Parse("req-body", raw, latencyMs: 50);

        var fail = outcome.Should().BeOfType<InsurerEmitOutcome.Failure>().Subject;
        fail.Error.ExternalCode.Should().Be("PARSE_ERROR");
    }

    [Fact]
    public void Inner_CDATA_xml_malformed_returns_PARSE_ERROR()
    {
        var raw = """
        <?xml version="1.0"?>
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <EmiteAxaResponse xmlns="http://tempuri.org/">
              <EmiteAxaResult><![CDATA[<RESPUESTA><url>no cierra]]></EmiteAxaResult>
            </EmiteAxaResponse>
          </soap:Body>
        </soap:Envelope>
        """;

        var outcome = AxaDxnCopsisEmitResponseParser.Parse("req-body", raw, latencyMs: 50);

        var fail = outcome.Should().BeOfType<InsurerEmitOutcome.Failure>().Subject;
        fail.Error.ExternalCode.Should().Be("PARSE_ERROR");
    }
}
