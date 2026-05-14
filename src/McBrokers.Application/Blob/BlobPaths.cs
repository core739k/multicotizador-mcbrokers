using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Blob;

public enum BlobRole { Request, Response }

// Centraliza la convención de paths para todos los blobs req/res/pdf que
// produce el sistema. Estructura:
//   {Year}/{Brand}/{Model}/{correlationId}/{kind}-{insurer}[-{ext}].{ext}
//
// El mismo path se usa en LocalDiskBlobStore (relativo a {root}) y en
// AzureBlobStore (como blob name dentro de un container fijo). Si el
// vendedor inspecciona logs/blobs/2024/ACURA/MDX/abc/ ve todos los
// artefactos de esa cotización en una sola carpeta — cotización +
// emisión + PDF.
public static class BlobPaths
{
    public static string Folder(int year, string brand, string model, string correlationId) =>
        $"{year}/{Sanitize(brand)}/{Sanitize(model)}/{correlationId}";

    public static string Cotizacion(int year, string brand, string model, string correlationId,
        InsurerCode insurer, BlobRole role) =>
        $"{Folder(year, brand, model, correlationId)}/cotizacion-{insurer}-{Suffix(role)}.xml";

    public static string Recotizacion(int year, string brand, string model, string correlationId,
        InsurerCode insurer, int version, string attemptId, BlobRole role) =>
        $"{Folder(year, brand, model, correlationId)}/cotizacion-{insurer}-v{version}-{attemptId}-{Suffix(role)}.xml";

    public static string Emision(int year, string brand, string model, string correlationId,
        InsurerCode insurer, string attemptId, BlobRole role) =>
        $"{Folder(year, brand, model, correlationId)}/emision-{insurer}-{attemptId}-{Suffix(role)}.xml";

    public static string PolizaPdf(int year, string brand, string model, string correlationId,
        InsurerCode insurer) =>
        $"{Folder(year, brand, model, correlationId)}/poliza-{insurer}.pdf";

    // Mayúsculas + replace de chars problemáticos para URLs y filesystems.
    // Espacios y / van a _ — Windows y Azure los aceptan pero los URL encoders
    // los rompen, mejor evitarlos en producción.
    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        return value.ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('/', '_')
            .Replace('\\', '_');
    }

    private static string Suffix(BlobRole role) => role switch
    {
        BlobRole.Request => "request",
        BlobRole.Response => "response",
        _ => "response",
    };
}
