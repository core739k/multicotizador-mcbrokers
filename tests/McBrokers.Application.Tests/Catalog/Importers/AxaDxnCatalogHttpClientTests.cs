using System.Net;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Application.Ports;
using McBrokers.Infrastructure.InsurerCatalogs;
using Microsoft.Extensions.Logging.Abstractions;

namespace McBrokers.Application.Tests.Catalog.Importers;

/// <summary>
/// El cliente HTTP de catálogo AXA: arma SOAP con AxaDxnCatalogSoapBuilder,
/// POST con Basic Auth, lee la respuesta y delega al parser. Convierte
/// AxaDxnCatalogRawRow (Insurers.AxaDxn) → AxaDxnCatalogRecord (Application).
/// Tests con HttpMessageHandler stub para capturar request y controlar response.
/// </summary>
public class AxaDxnCatalogHttpClientTests
{
    private const string Endpoint = "https://serviciosweb.example/EmisionPolizasWS/services/SolicitudPolizasService";

    private static readonly AxaDxnCatalogCredentials Creds = new(
        Usuario: "USER01", Password: "secret-pw", EndpointUrl: Endpoint);

    private static readonly string ValidMarcaSoap = $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <ns:getCatalogosPorTarifaYNombreResponse xmlns:ns="https://agentes.axa.com.mx/EmisionPolizasWS/services/SolicitudPolizasService">
              <getCatalogosPorTarifaYNombreReturn>&lt;catalogos&gt;&lt;catalogo&gt;&lt;registro&gt;&lt;campo nombre="idMarca" valor="42"/&gt;&lt;campo nombre="idTipoVehiculo" valor="1"/&gt;&lt;campo nombre="descripcion" valor="TOYOTA"/&gt;&lt;/registro&gt;&lt;/catalogo&gt;&lt;/catalogos&gt;</getCatalogosPorTarifaYNombreReturn>
            </ns:getCatalogosPorTarifaYNombreResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static AxaDxnCatalogHttpClient Build(StubHandler handler) =>
        new(new HttpClient(handler), NullLogger<AxaDxnCatalogHttpClient>.Instance);

    [Fact]
    public async Task Successful_response_posts_to_endpoint_with_basic_auth_and_soap_body()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ValidMarcaSoap);

        var result = await Build(handler).FetchAsync(Creds, "TAR-AUTOS", "Marca", CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be(Endpoint);

        handler.LastRequest.Headers.Authorization.Should().NotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        var expectedAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes("USER01:secret-pw"));
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be(expectedAuth);

        handler.LastRequestBody.Should().Contain("getCatalogosPorTarifaYNombre");
        handler.LastRequestBody.Should().Contain("TAR-AUTOS");
        handler.LastRequestBody.Should().Contain("Marca");

        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("text/xml");
    }

    [Fact]
    public async Task Successful_response_parses_and_maps_records_to_application_dto()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ValidMarcaSoap);

        var result = await Build(handler).FetchAsync(Creds, "TAR-AUTOS", "Marca", CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().ContainSingle();

        var row = result.Value[0];
        row.IdMarca.Should().Be("42");
        row.IdTipoVehiculo.Should().Be("1");
        row.Descripcion.Should().Be("TOYOTA");
    }

    [Fact]
    public async Task Http_500_returns_HTTP_500_failure()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "<error/>");

        var result = await Build(handler).FetchAsync(Creds, "TAR-AUTOS", "Marca", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("HTTP_500");
    }

    [Fact]
    public async Task Network_error_returns_HTTP_ERROR_failure()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        var result = await Build(handler).FetchAsync(Creds, "TAR-AUTOS", "Marca", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("HTTP_ERROR");
        result.Error.Should().Contain("connection refused");
    }

    [Fact]
    public async Task Parser_failure_propagates_as_failure_result()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "<not-xml at all");

        var result = await Build(handler).FetchAsync(Creds, "TAR-AUTOS", "Marca", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("PARSE_ERROR");
    }

    // -------------------- helper --------------------

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        public StubHandler(HttpStatusCode status, string responseBody)
            : this(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "text/xml"),
            })
        {
        }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responder(request);
        }
    }
}
