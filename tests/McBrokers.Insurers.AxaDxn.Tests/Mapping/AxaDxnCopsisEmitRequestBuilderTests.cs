using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.AxaDxn.Mapping;

namespace McBrokers.Insurers.AxaDxn.Tests.Mapping;

/// <summary>
/// El body que COPSIS espera es JSON con {s4_key, b, v1} donde v1 es un XML SOLICITUDEMISION
/// que embebe el XML de respuesta de cotización + datos contratante/vehículo. Esto replica el
/// contrato del legacy (CotizacionNegocio.cs:5333-5391). Tests sobre el builder de v1.
/// </summary>
public class AxaDxnCopsisEmitRequestBuilderTests
{
    private static readonly AxaDxnAdapterConfig SampleAxa = new(
        Usuario: "MXS00102308A",
        Password: "secret",
        Tarifa: "RES",
        TarifaPickup: "PCK",
        Descuento: 0,
        DescuentoPickup: 0,
        MesPolizaDefault: 5,
        SelectedBusinessName: "Strm",
        PolizaAutos: "POL000999",
        PolizaPickup: null,
        BusinessMes: 5,
        CopsisD4Key: "d4-real-key",
        CopsisB: "b-real-param");

    private static InsurerEmitRequest SampleEmit(
        ValuationType valuation = ValuationType.Commercial,
        string? rawQuoteXml = null,
        string? agentCode = null) =>
        new(
            CorrelationId: "corr-001",
            Credentials: new InsurerCredentials("u", "p", null),
            Connection: new InsurerConnectionConfig("https://example", 30, 1),
            ExternalQuoteRef: "AXA-FOLIO-123",
            Vehicle: new EmissionVehicleData(2024, "ACURA", "MDX", "BASE", "01112233",
                Plate: "RRR123", EngineNumber: "MOT-X", SerialNumber: "SER-9"),
            Contractor: new EmissionContactData(
                FirstName: "JOSÉ", LastNamePaternal: "DURÁN", LastNameMaternal: "PEREZ",
                Rfc: "RABY881201VF5", Street: "CENTLAPATL", ExteriorNumber: "143",
                InteriorNumber: "601A", Neighborhood: "SAN MARTÍN",
                City: "GUADALUPE", StateCode: "NUEVO LEON", PostalCode: "06700",
                Phone: "5565465467", Email: "test@x.com"),
            HabitualDriver: new EmissionContactData(
                "JOSÉ", "DURÁN", "PEREZ", "RABY881201VF5", "CENTLAPATL", "143", null,
                "SAN MARTÍN", "GUADALUPE", "NUEVO LEON", "06700", "5565465467", "test@x.com"),
            PremiumTotal: 10000m, PremiumNet: 8500m, Tax: 1360m, Fees: 140m,
            Valuation: valuation,
            RawQuoteResponseXml: rawQuoteXml,
            AgentExternalCode: agentCode,
            BusinessConfig: SampleAxa);

    [Fact]
    public void SOLICITUDEMISION_root_has_hardcoded_clientews_275()
    {
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(SampleEmit(), SampleAxa);

        var doc = XDocument.Parse(v1);
        doc.Root!.Name.LocalName.Should().Be("SOLICITUDEMISION");
        doc.Descendants("clientews").Single().Value.Should().Be("275");
    }

    [Fact]
    public void Uses_PolizaAutos_from_config()
    {
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(SampleEmit(), SampleAxa);
        XDocument.Parse(v1).Descendants("numeroPoliza").Single().Value.Should().Be("POL000999");
    }

    [Theory]
    [InlineData(ValuationType.Commercial, "Comercial", "100")]
    [InlineData(ValuationType.CommercialPlus10, "Comercial", "110")]
    [InlineData(ValuationType.Agreed, "Convenido", "100")]
    [InlineData(ValuationType.AgreedPlus10, "Convenido", "110")]
    [InlineData(ValuationType.Invoice, "Factura", "100")]
    public void Maps_tipoValor_and_porcentajeValor(ValuationType v, string tipo, string pct)
    {
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(SampleEmit(v), SampleAxa);

        var doc = XDocument.Parse(v1);
        doc.Descendants("tipoValor").Single().Value.Should().Be(tipo);
        doc.Descendants("porcentajeValor").Single().Value.Should().Be(pct);
    }

    [Fact]
    public void Contractor_fields_are_mapped()
    {
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(SampleEmit(), SampleAxa);
        var d = XDocument.Parse(v1);

        d.Descendants("nombreConductor").Single().Value.Should().Be("JOSÉ");
        d.Descendants("paterno").Single().Value.Should().Be("DURÁN");
        d.Descendants("materno").Single().Value.Should().Be("PEREZ");
        d.Descendants("rfc").Single().Value.Should().Be("RABY881201VF5");
        d.Descendants("cp").Single().Value.Should().Be("06700");
        d.Descendants("colonia").Single().Value.Should().Be("SAN MARTÍN");
        d.Descendants("edo").Single().Value.Should().Be("NUEVO LEON");
        d.Descendants("mun").Single().Value.Should().Be("GUADALUPE");
        // Dirección concatena calle + numExterior como en legacy.
        d.Descendants("direccion").Single().Value.Should().Be("CENTLAPATL 143");
    }

    [Fact]
    public void Vehicle_fields_are_mapped()
    {
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(SampleEmit(), SampleAxa);
        var d = XDocument.Parse(v1);

        d.Descendants("serie").Single().Value.Should().Be("SER-9");
        d.Descendants("placas").Single().Value.Should().Be("RRR123");
        d.Descendants("motor").Single().Value.Should().Be("MOT-X");
        d.Descendants("estadoPlacas").Single().Value.Should().Be("NUEVO LEON");
    }

    [Fact]
    public void Embeds_raw_quote_response_inside_CotizarIncisoResponse_wrapper()
    {
        // Legacy quita el wrapper soapenv del XML de cotización y mete el contenido crudo
        // dentro de CotizaAutoRespuesta/CotizarIncisoResponse. Si pasamos el XML completo
        // de cotización, el builder debe quitarle los wrappers y dejar solo el cuerpo.
        const string rawQuote = """
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Header/><soapenv:Body><ns2:CotizarIncisoResponse xmlns:ns2="http://axa.com.mx/autos/flotillas/ws"><folio>AXA-FOLIO-123</folio><primaTotal>10000</primaTotal></ns2:CotizarIncisoResponse></soapenv:Body></soapenv:Envelope>
            """;

        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(
            SampleEmit(rawQuoteXml: rawQuote), SampleAxa);

        // El v1 NO debe contener el soapenv:Envelope envolvente.
        v1.Should().NotContain("soapenv:Envelope");
        // Pero debe envolver el contenido en CotizaAutoRespuesta/CotizarIncisoResponse.
        v1.Should().Contain("<CotizaAutoRespuesta>");
        v1.Should().Contain("<CotizarIncisoResponse>");
        // Y debe traer el folio embebido como muestra de que se metió el body crudo.
        v1.Should().Contain("AXA-FOLIO-123");
    }

    [Fact]
    public void Builds_valid_xml_when_raw_quote_is_null()
    {
        // Sin XML de cotización todavía debe armar v1 parseable — sólo CotizarIncisoResponse vacío.
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(
            SampleEmit(rawQuoteXml: null), SampleAxa);

        var doc = XDocument.Parse(v1);
        doc.Descendants("CotizarIncisoResponse").Should().ContainSingle();
    }

    [Fact]
    public void Vendedor_uses_AgentExternalCode_when_provided()
    {
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(
            SampleEmit(agentCode: "AGE-9999"), SampleAxa);

        XDocument.Parse(v1).Descendants("vendedor").Single().Value.Should().Be("AGE-9999");
    }
}
