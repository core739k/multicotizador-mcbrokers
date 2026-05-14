using System.Net;
using System.Text;
using McBrokers.Application.Postal;
using McBrokers.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;

namespace McBrokers.Application.Tests.Postal;

// SepomexHttpResolver vive en Infrastructure (depende de HttpClient) pero
// lo testeamos aquí porque Application.Tests es el proyecto más cómodo
// para tests con mocks de HttpMessageHandler. No requiere arrancar el host.
public class SepomexHttpResolverTests
{
    private const string BaseUrl = "https://consultarcp-api.azurewebsites.net";

    private static SepomexHttpResolver BuildResolver(HttpResponseMessage canned)
    {
        var handler = new StubHandler(canned);
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        return new SepomexHttpResolver(http, NullLogger<SepomexHttpResolver>.Instance);
    }

    [Fact]
    public async Task Returns_parsed_info_on_200()
    {
        var json = """
            {
              "CP": "54020",
              "Estado": "México",
              "Municipio": "Tlalnepantla de Baz",
              "Asentamientos": [
                { "Colonia": "Valle Dorado", "Tipo_Asentamiento": "Fraccionamiento", "Es_Manual": false },
                { "Colonia": "Tequexquináhuac", "Tipo_Asentamiento": "Pueblo", "Es_Manual": false }
              ]
            }
            """;
        var resolver = BuildResolver(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var result = await resolver.ResolveAsync("54020", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CodigoPostal.Should().Be("54020");
        result.Value.Estado.Should().Be("México");
        result.Value.Municipio.Should().Be("Tlalnepantla de Baz");
        result.Value.Asentamientos.Should().HaveCount(2);
        result.Value.Asentamientos[0].Colonia.Should().Be("Valle Dorado");
        result.Value.Asentamientos[0].TipoAsentamiento.Should().Be("Fraccionamiento");
    }

    [Fact]
    public async Task Returns_failure_on_404()
    {
        var resolver = BuildResolver(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await resolver.ResolveAsync("00000", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrado", because: "the error message should be user-readable in Spanish");
    }

    [Fact]
    public async Task Returns_failure_on_5xx()
    {
        var resolver = BuildResolver(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await resolver.ResolveAsync("54020", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SEPOMEX");
    }

    [Fact]
    public async Task Returns_failure_when_response_is_not_json()
    {
        var resolver = BuildResolver(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "text/plain"),
        });

        var result = await resolver.ResolveAsync("54020", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("abcde")]
    [InlineData("123456")]
    public async Task Rejects_invalid_cp_format_without_calling_remote(string? cp)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        var resolver = new SepomexHttpResolver(http, NullLogger<SepomexHttpResolver>.Instance);

        var result = await resolver.ResolveAsync(cp!, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        handler.CallsReceived.Should().Be(0, because: "invalid CP shouldn't reach the remote");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public int CallsReceived { get; private set; }

        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallsReceived++;
            return Task.FromResult(_response);
        }
    }
}
