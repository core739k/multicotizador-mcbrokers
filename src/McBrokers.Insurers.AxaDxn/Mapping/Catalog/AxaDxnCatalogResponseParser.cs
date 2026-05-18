using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using McBrokers.SharedKernel;

namespace McBrokers.Insurers.AxaDxn.Mapping.Catalog;

public static class AxaDxnCatalogResponseParser
{
    public static Result<IReadOnlyList<AxaDxnCatalogRawRow>> Parse(string soapXml)
    {
        XDocument outer;
        try { outer = XDocument.Parse(soapXml); }
        catch (XmlException ex)
        {
            return Result<IReadOnlyList<AxaDxnCatalogRawRow>>.Failure($"PARSE_ERROR: outer xml malformed - {ex.Message}");
        }

        var fault = outer.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value
                              ?? "unknown";
            return Result<IReadOnlyList<AxaDxnCatalogRawRow>>.Failure($"SOAP_FAULT: {faultString}");
        }

        var returnNode = outer.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "getCatalogosPorTarifaYNombreReturn");
        if (returnNode is null)
        {
            return Result<IReadOnlyList<AxaDxnCatalogRawRow>>.Failure(
                "PARSE_ERROR: missing getCatalogosPorTarifaYNombreReturn node");
        }

        var innerXml = returnNode.Value;
        if (string.IsNullOrWhiteSpace(innerXml))
        {
            return Result<IReadOnlyList<AxaDxnCatalogRawRow>>.Failure(
                "PARSE_ERROR: empty getCatalogosPorTarifaYNombreReturn content");
        }

        XDocument inner;
        try { inner = XDocument.Parse(innerXml); }
        catch (XmlException ex)
        {
            return Result<IReadOnlyList<AxaDxnCatalogRawRow>>.Failure(
                $"PARSE_ERROR: inner xml malformed - {ex.Message}");
        }

        var rows = new List<AxaDxnCatalogRawRow>();
        foreach (var registro in inner.Descendants("registro"))
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var campo in registro.Elements("campo"))
            {
                var name = campo.Attribute("nombre")?.Value ?? campo.Element("nombre")?.Value;
                var value = campo.Attribute("valor")?.Value ?? campo.Element("valor")?.Value;
                if (!string.IsNullOrEmpty(name) && value is not null)
                {
                    fields[name] = value;
                }
            }

            rows.Add(new AxaDxnCatalogRawRow(
                IdMarca: fields.GetValueOrDefault("idMarca"),
                IdTipoVehiculo: fields.GetValueOrDefault("idTipoVehiculo"),
                Descripcion: fields.GetValueOrDefault("descripcion"),
                IdTipo: fields.GetValueOrDefault("idTipo"),
                ClaveAmis: fields.GetValueOrDefault("claveAMIS"),
                ModeloDesde: TryParseInt(fields.GetValueOrDefault("modeloDesde")),
                ModeloHasta: TryParseInt(fields.GetValueOrDefault("modeloHasta"))));
        }

        return Result<IReadOnlyList<AxaDxnCatalogRawRow>>.Success(rows);
    }

    private static int? TryParseInt(string? raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
