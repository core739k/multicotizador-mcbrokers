using McBrokers.Insurers.AxaDxn.Mapping.Catalog;

namespace McBrokers.Insurers.AxaDxn.Tests.Mapping.Catalog;

/// <summary>
/// El WS de AXA (getCatalogosPorTarifaYNombre) devuelve SOAP con un nodo
/// &lt;getCatalogosPorTarifaYNombreReturn&gt; cuyo contenido es XML escapado con
/// entidades (&amp;lt; / &amp;gt;). XDocument desescapa al acceder .Value, así
/// que el parser hace doble Parse: outer SOAP → inner catalogos.
/// </summary>
public class AxaDxnCatalogResponseParserTests
{
    private static string LoadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RecordedCatalogResponses", fileName));

    [Fact]
    public void Marca_response_parses_two_records_with_idMarca_idTipoVehiculo_descripcion()
    {
        var raw = LoadFixture("marca_two_records.xml");

        var result = AxaDxnCatalogResponseParser.Parse(raw);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().HaveCount(2);

        result.Value[0].IdMarca.Should().Be("1");
        result.Value[0].IdTipoVehiculo.Should().Be("1");
        result.Value[0].Descripcion.Should().Be("TOYOTA");
        result.Value[0].IdTipo.Should().BeNull();
        result.Value[0].ClaveAmis.Should().BeNull();
        result.Value[0].ModeloDesde.Should().BeNull();
        result.Value[0].ModeloHasta.Should().BeNull();

        result.Value[1].IdMarca.Should().Be("2");
        result.Value[1].Descripcion.Should().Be("HONDA");
    }

    [Fact]
    public void Submarca_response_parses_all_fields_including_modelo_range_and_claveAmis()
    {
        var raw = LoadFixture("submarca_one_record.xml");

        var result = AxaDxnCatalogResponseParser.Parse(raw);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().HaveCount(1);

        var row = result.Value[0];
        row.IdMarca.Should().Be("1");
        row.IdTipoVehiculo.Should().Be("2");
        row.Descripcion.Should().Be("COROLLA SE 1.8L");
        row.IdTipo.Should().Be("5");
        row.ClaveAmis.Should().Be("01234");
        row.ModeloDesde.Should().Be(2020);
        row.ModeloHasta.Should().Be(2028);
    }

    [Fact]
    public void Values_with_xml_entities_are_unescaped_correctly()
    {
        // El valor &amp;amp; debe llegar como "&" en la descripción final
        // (doble unescape: SOAP outer → inner content → atributo).
        var raw = LoadFixture("values_with_entities.xml");

        var result = AxaDxnCatalogResponseParser.Parse(raw);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().ContainSingle();
        result.Value[0].Descripcion.Should().Be("MERCEDES-BENZ E-CLASS & SPORT");
    }

    [Fact]
    public void Missing_return_node_yields_PARSE_ERROR()
    {
        var raw = LoadFixture("missing_return_node.xml");

        var result = AxaDxnCatalogResponseParser.Parse(raw);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("PARSE_ERROR");
    }

    [Fact]
    public void Soap_fault_yields_SOAP_FAULT_with_faultstring()
    {
        var raw = LoadFixture("soap_fault.xml");

        var result = AxaDxnCatalogResponseParser.Parse(raw);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("SOAP_FAULT");
        result.Error.Should().Contain("usuario invalido");
    }

    [Fact]
    public void Malformed_outer_xml_yields_PARSE_ERROR()
    {
        var result = AxaDxnCatalogResponseParser.Parse("<not-xml at all");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("PARSE_ERROR");
    }
}
