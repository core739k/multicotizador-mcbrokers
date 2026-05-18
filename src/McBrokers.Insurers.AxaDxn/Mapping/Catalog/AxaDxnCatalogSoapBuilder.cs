using System.Xml.Linq;

namespace McBrokers.Insurers.AxaDxn.Mapping.Catalog;

/// <summary>
/// Genera el envelope SOAP 1.1 estilo RPC/encoded que espera el WS de catálogo de AXA
/// (Apache Axis 1.x — namespace ia3.ing.com). Replica exactamente la forma del legacy
/// (CatalogoVehiculosNegocio.cs:1519-1531): xmlns:xsi/xsd en el Envelope,
/// soapenv:encodingStyle en la operación y xsi:type="xsd:string" en cada parámetro.
/// Sin esos atributos el deserializer Java rechaza el request con HTTP 500.
/// </summary>
public static class AxaDxnCatalogSoapBuilder
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace SoapEncoding = "http://schemas.xmlsoap.org/soap/encoding/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Wsf = "http://wsfacade.emisionpolizas.autos.seguros.mx.ia3.ing.com";

    public static string Build(string tarifa, string nombreCatalogo)
    {
        if (string.IsNullOrWhiteSpace(tarifa))
        {
            throw new ArgumentException("tarifa must not be empty.", nameof(tarifa));
        }

        if (string.IsNullOrWhiteSpace(nombreCatalogo))
        {
            throw new ArgumentException("nombreCatalogo must not be empty.", nameof(nombreCatalogo));
        }

        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsf", Wsf.NamespaceName),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body",
                new XElement(Wsf + "getCatalogosPorTarifaYNombre",
                    new XAttribute(Soap + "encodingStyle", SoapEncoding.NamespaceName),
                    new XElement("tarifa",
                        new XAttribute(Xsi + "type", "xsd:string"),
                        tarifa),
                    new XElement("nombreCatalogo",
                        new XAttribute(Xsi + "type", "xsd:string"),
                        nombreCatalogo))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope).ToString(SaveOptions.DisableFormatting);
    }
}
