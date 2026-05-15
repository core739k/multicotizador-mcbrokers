using System.Net;
using McBrokers.Api.Tests.Testing;
using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;
using McBrokers.Domain.Emissions;
using McBrokers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Api.Tests.Emissions;

// Visor de póliza: GET /api/v1/emissions/{id}/pdf resuelve PdfBlobRef via
// IBlobStore.ReadBinaryAsync y devuelve application/pdf inline. Auth obligatoria
// — un agente sin sesión no puede leer pólizas de nadie.
public class EmissionsPdfEndpointTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;
    public EmissionsPdfEndpointTests(AdminApiFactory factory) => _factory = factory;

    private async Task<AppDbContext> ResetDbAsync()
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EmissionAttempts.RemoveRange(db.EmissionAttempts);
        db.Emissions.RemoveRange(db.Emissions);
        await db.SaveChangesAsync();
        return db;
    }

    private static Emission BuildIssuedEmission(string? pdfBlobRef, string? policyNumber = "4")
    {
        var emission = Emission.Start(
            quotationInsurerResultId: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow).Value;
        if (policyNumber is not null)
        {
            emission.MarkIssued(policyNumber, pdfBlobRef, DateTime.UtcNow);
        }
        return emission;
    }

    [Fact]
    public async Task Returns_pdf_inline_with_bytes_when_emission_is_issued()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var db = await ResetDbAsync();

        // Escribe el blob por el path canónico (BlobPaths.PolizaPdf) usando
        // el mismo IBlobStore que servirá el endpoint — así nos aseguramos
        // que la convención path-relativa funciona end-to-end.
        var blob = _factory.Services.GetRequiredService<IBlobStore>();
        const string pdfPath = "2024/ACURA/MDX/cid-test/poliza-AxaDxn.pdf";
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        await blob.WriteBinaryAsync(pdfPath, bytes, metadata: null, CancellationToken.None);

        var emission = BuildIssuedEmission(pdfPath);
        db.Emissions.Add(emission);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/emissions/{emission.Id}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("inline");
        var returned = await response.Content.ReadAsByteArrayAsync();
        returned.Should().Equal(bytes);
    }

    [Fact]
    public async Task Returns_404_when_emission_not_found()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        await ResetDbAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/emissions/{Guid.NewGuid()}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Returns_404_when_pdf_blob_ref_is_null()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var db = await ResetDbAsync();
        var emission = BuildIssuedEmission(pdfBlobRef: null);
        db.Emissions.Add(emission);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/emissions/{emission.Id}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Returns_404_when_blob_is_missing_on_disk()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var db = await ResetDbAsync();
        // PdfBlobRef apunta a un path que no existe físicamente — caso de
        // emisión vieja con blob borrado o aún no descargado.
        var emission = BuildIssuedEmission("2024/NOPE/NOPE/cid-missing/poliza-AxaDxn.pdf");
        db.Emissions.Add(emission);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/emissions/{emission.Id}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Returns_401_or_redirect_when_anonymous()
    {
        await using var anon = new AnonymousApiFactory();
        using var client = anon.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/emissions/{Guid.NewGuid()}/pdf");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    private sealed class AnonymousApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", "Server=unreachable;Database=test;");
            builder.UseSetting("Authentication:Google:ClientId", "test-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-secret");
        }
    }
}
