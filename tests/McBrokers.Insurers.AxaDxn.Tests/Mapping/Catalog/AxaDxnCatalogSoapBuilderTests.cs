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
