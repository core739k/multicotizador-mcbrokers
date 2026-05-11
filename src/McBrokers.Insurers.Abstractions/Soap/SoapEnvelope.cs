using System.Text;
using System.Xml.Linq;

namespace McBrokers.Insurers.Abstractions.Soap;

/// <summary>
/// Helpers para construir y desempaquetar envelopes SOAP 1.1/1.2 con HttpClient
/// (sin necesidad de generar proxies WCF). Cada adapter conoce el namespace y
/// método de su aseguradora; este helper sólo envuelve y extrae el body.
/// </summary>
public static class SoapEnvelope
{
    public const string Soap11Namespace = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string Soap12Namespace = "http://www.w3.org/2003/05/soap-envelope";

    public static string Wrap(SoapVersion version, XElement bodyContent)
    {
        var soapNs = version == SoapVersion.Soap12 ? Soap12Namespace : Soap11Namespace;
        var soap = XNamespace.Get(soapNs);

        var envelope = new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", soapNs),
            new XElement(soap + "Header"),
            new XElement(soap + "Body", bodyContent));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Devuelve el primer hijo del Body como XElement (el wrapper específico del método).
    /// </summary>
    public static XElement? ExtractBodyChild(string rawSoapResponse)
    {
        var doc = XDocument.Parse(rawSoapResponse);
        return doc.Descendants()
            .FirstOrDefault(e =>
                e.Parent is not null
                && (e.Parent.Name.LocalName == "Body")
                && (e.Parent.Name.NamespaceName is Soap11Namespace or Soap12Namespace));
    }

    public static string MediaType(SoapVersion version) =>
        version == SoapVersion.Soap12 ? "application/soap+xml" : "text/xml";
}

public enum SoapVersion
{
    Soap11 = 11,
    Soap12 = 12,
}
