using McBrokers.Application.Blob;
using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Tests.Blob;

public class BlobPathsTests
{
    [Fact]
    public void Folder_uses_year_brand_model_correlationId_layout()
    {
        var path = BlobPaths.Folder(2024, "ACURA", "MDX", "abc-corr-123");
        path.Should().Be("2024/ACURA/MDX/abc-corr-123");
    }

    [Fact]
    public void Folder_uppercases_brand_and_model()
    {
        var path = BlobPaths.Folder(2024, "vw", "jetta", "abc");
        path.Should().Be("2024/VW/JETTA/abc");
    }

    [Fact]
    public void Folder_replaces_unsafe_chars_in_brand_and_model()
    {
        // Brand "AXA COL" tiene espacio — el path debe ser usable como path real
        // (Windows + Azure aceptan espacios pero los URLs encoders los rompen;
        // usamos _ para evitar fricción operativa).
        var path = BlobPaths.Folder(2024, "AXA COL", "Modelo / con slash", "corr");
        path.Should().Be("2024/AXA_COL/MODELO___CON_SLASH/corr");
    }

    [Fact]
    public void Cotizacion_request_combines_folder_with_insurer_in_filename()
    {
        var path = BlobPaths.Cotizacion(2024, "VW", "JETTA", "corr-xyz", InsurerCode.Gnp, role: BlobRole.Request);
        path.Should().Be("2024/VW/JETTA/corr-xyz/cotizacion-Gnp-request.xml");
    }

    [Fact]
    public void Cotizacion_response_same_pattern_with_response_suffix()
    {
        var path = BlobPaths.Cotizacion(2024, "VW", "JETTA", "corr-xyz", InsurerCode.AxaDxn, role: BlobRole.Response);
        path.Should().Be("2024/VW/JETTA/corr-xyz/cotizacion-AxaDxn-response.xml");
    }

    [Fact]
    public void Recotizacion_includes_version_and_attempt_guid()
    {
        var path = BlobPaths.Recotizacion(2024, "VW", "JETTA", "corr",
            InsurerCode.Qua, version: 2, attemptId: "abc123", role: BlobRole.Request);
        path.Should().Be("2024/VW/JETTA/corr/cotizacion-Qua-v2-abc123-request.xml");
    }

    [Fact]
    public void Emision_includes_attempt_guid_and_role()
    {
        var path = BlobPaths.Emision(2024, "VW", "JETTA", "corr",
            InsurerCode.AxaDxn, attemptId: "xyz789", role: BlobRole.Response);
        path.Should().Be("2024/VW/JETTA/corr/emision-AxaDxn-xyz789-response.xml");
    }

    [Fact]
    public void Poliza_pdf_lives_alongside_emit_files()
    {
        var path = BlobPaths.PolizaPdf(2024, "VW", "JETTA", "corr", InsurerCode.AxaDxn);
        path.Should().Be("2024/VW/JETTA/corr/poliza-AxaDxn.pdf");
    }
}
