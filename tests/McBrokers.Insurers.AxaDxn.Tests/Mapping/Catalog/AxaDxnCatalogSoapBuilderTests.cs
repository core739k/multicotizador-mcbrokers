using System.Xml.Linq;
using McBrokers.Insurers.AxaDxn.Mapping.Catalog;

namespace McBrokers.Insurers.AxaDxn.Tests.Mapping.Catalog;

/// <summary>
/// El builder genera el envelope SOAP 1.1 con namespace wsf
/// (http://wsfacade.emisionpolizas.autos.seguros.mx.ia3.ing.com) y body
/// getCatalogosPorTarifaYNombre con elementos tarifa y nombreCatalogo,
/// igual que el legacy CatalogoVehiculosNegocio:1519-1531.
/// </summary>
public class AxaDxnCatalogSoapBuilderTests
{
    [Fact]
    public void Builds_valid_soap_envelope_with_tarifa_and_nombreCatalogo()
    {
        var soap = AxaDxnCatalogSoapBuilder.Build("MX001-AUTOS", "Marca");

        var doc = XDocument.Parse(soap);
        var body = doc.Descendants().Single(e => e.Name.LocalName == "Body");
        var call = body.Elements().Single();

        call.Name.LocalName.Should().Be("getCatalogosPorTarifaYNombre");
        call.Name.NamespaceName.Should().Be("http://wsfacade.emisionpolizas.autos.seguros.mx.ia3.ing.com");

        call.Elements().Single(e => e.Name.LocalName == "tarifa").Value.Should().Be("MX001-AUTOS");
        call.Elements().Single(e => e.Name.LocalName == "nombreCatalogo").Value.Should().Be("Marca");
    }

    [Fact]
    public void Builds_envelope_with_Submarca_when_requested()
    {
        var soap = AxaDxnCatalogSoapBuilder.Build("MX001-AUTOS", "Submarca");

        XDocument.Parse(soap)
            .Descendants().Single(e => e.Name.LocalName == "nombreCatalogo")
            .Value.Should().Be("Submarca");
    }

    [Fact]
    public void Special_xml_characters_in_tarifa_are_escaped()
    {
        var soap = AxaDxnCatalogSoapBuilder.Build("FOO&BAR<X>", "Marca");

        var doc = XDocument.Parse(soap);
        doc.Descendants().Single(e => e.Name.LocalName == "tarifa").Value.Should().Be("FOO&BAR<X>");
        soap.Should().NotContain("FOO&BAR<X>", "raw '<' inside an attribute/element value must be escaped to &lt;");
    }

    [Fact]
    public void Operation_carries_soap_encoding_style_attribute_for_axis_rpc_encoded()
    {
        // Apache Axis 1.x rechaza con HTTP 500 si falta soapenv:encodingStyle="...soap/encoding/".
        // El legacy CatalogoVehiculosNegocio:1525 lo incluye literal.
        var soap = AxaDxnCatalogSoapBuilder.Build("TB7144", "Marca");

        var doc = XDocument.Parse(soap);
        var op = doc.Descendants().Single(e => e.Name.LocalName == "getCatalogosPorTarifaYNombre");

        var encodingStyle = op.Attribute(
            XName.Get("encodingStyle", "http://schemas.xmlsoap.org/soap/envelope/"));
        encodingStyle.Should().NotBeNull();
        encodingStyle!.Value.Should().Be("http://schemas.xmlsoap.org/soap/encoding/");
    }

    [Fact]
    public void Parameters_carry_xsi_type_xsd_string_for_rpc_encoded_deserialization()
    {
        // Axis 1.x usa xsi:type para resolver el binding de los parámetros en RPC/encoded.
        var soap = AxaDxnCatalogSoapBuilder.Build("TB7144", "Marca");

        var doc = XDocument.Parse(soap);
        var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

        var tarifa = doc.Descendants().Single(e => e.Name.LocalName == "tarifa");
        tarifa.Attribute(xsi + "type")!.Value.Should().Be("xsd:string");

        var nombreCatalogo = doc.Descendants().Single(e => e.Name.LocalName == "nombreCatalogo");
        nombreCatalogo.Attribute(xsi + "type")!.Value.Should().Be("xsd:string");
    }

    [Fact]
    public void Envelope_declares_xsi_and_xsd_namespaces_for_type_hint_resolution()
    {
        // El prefix "xsd" en xsi:type="xsd:string" debe estar declarado en el Envelope,
        // o el parser Axis no resuelve el namespace y rechaza el request.
        var soap = AxaDxnCatalogSoapBuilder.Build("TB7144", "Marca");

        var doc = XDocument.Parse(soap);
        var envelope = doc.Root!;
        var xmlns = XNamespace.Xmlns;

        envelope.Attribute(xmlns + "xsi")!.Value.Should().Be("http://www.w3.org/2001/XMLSchema-instance");
        envelope.Attribute(xmlns + "xsd")!.Value.Should().Be("http://www.w3.org/2001/XMLSchema");
    }

    [Theory]
    [InlineData(null, "Marca")]
    [InlineData("", "Marca")]
    [InlineData("  ", "Marca")]
    public void Empty_tarifa_throws_ArgumentException(string? tarifa, string nombreCatalogo)
    {
        var act = () => AxaDxnCatalogSoapBuilder.Build(tarifa!, nombreCatalogo);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("tarifa");
    }

    [Theory]
    [InlineData("MX001", null)]
    [InlineData("MX001", "")]
    [InlineData("MX001", " ")]
    public void Empty_nombreCatalogo_throws_ArgumentException(string tarifa, string? nombreCatalogo)
    {
        var act = () => AxaDxnCatalogSoapBuilder.Build(tarifa, nombreCatalogo!);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("nombreCatalogo");
    }
}
