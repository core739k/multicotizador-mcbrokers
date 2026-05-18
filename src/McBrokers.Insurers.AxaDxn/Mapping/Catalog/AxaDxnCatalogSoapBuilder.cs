using System.Xml.Linq;

namespace McBrokers.Insurers.AxaDxn.Mapping.Catalog;

public static class AxaDxnCatalogSoapBuilder
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
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
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsf", Wsf.NamespaceName),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body",
                new XElement(Wsf + "getCatalogosPorTarifaYNombre",
                    new XElement("tarifa", tarifa),
                    new XElement("nombreCatalogo", nombreCatalogo))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope).ToString(SaveOptions.DisableFormatting);
    }
}
