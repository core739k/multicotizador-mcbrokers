using McBrokers.Insurers.Ana;

namespace McBrokers.Insurers.Ana.Tests;

public class AnaPostalCodeResolverTests
{
    [Fact]
    public void Parses_ColoniaxCP_response_with_inline_colonia()
    {
        var raw = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ColoniaxCPResponse xmlns="http://server.anaseguros.com.mx/WSCOR/">
                  <ColoniaxCPResult>
                    <colonia>
                      <IdEstado>9</IdEstado>
                      <IdDelMun>2</IdDelMun>
                      <DelMun>BENITO JUAREZ</DelMun>
                    </colonia>
                  </ColoniaxCPResult>
                </ColoniaxCPResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var info = AnaPostalCodeResolver.ParseResponse(raw);

        info.Should().NotBeNull();
        info!.EdoMun.Should().Be("09002");
        info.Poblacion.Should().Be("BENITO JUAREZ");
    }

    [Fact]
    public void Parses_ColoniaxCP_response_with_embedded_xml_string()
    {
        var raw = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ColoniaxCPResponse xmlns="http://server.anaseguros.com.mx/WSCOR/">
                  <ColoniaxCPResult>&lt;colonias&gt;&lt;colonia&gt;&lt;IdEstado&gt;19&lt;/IdEstado&gt;&lt;IdDelMun&gt;39&lt;/IdDelMun&gt;&lt;DelMun&gt;MONTERREY&lt;/DelMun&gt;&lt;/colonia&gt;&lt;/colonias&gt;</ColoniaxCPResult>
                </ColoniaxCPResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var info = AnaPostalCodeResolver.ParseResponse(raw);

        info.Should().NotBeNull();
        info!.EdoMun.Should().Be("19039");
        info.Poblacion.Should().Be("MONTERREY");
    }

    [Fact]
    public void Returns_null_when_response_lacks_colonia()
    {
        var raw = "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ColoniaxCPResponse><ColoniaxCPResult></ColoniaxCPResult></ColoniaxCPResponse></soap:Body></soap:Envelope>";

        AnaPostalCodeResolver.ParseResponse(raw).Should().BeNull();
    }

    [Fact]
    public void Returns_null_on_malformed_xml()
    {
        AnaPostalCodeResolver.ParseResponse("<<<not xml>>>").Should().BeNull();
    }
}
