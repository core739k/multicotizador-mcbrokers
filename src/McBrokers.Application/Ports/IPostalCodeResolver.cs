using McBrokers.SharedKernel;

namespace McBrokers.Application.Ports;

public interface IPostalCodeResolver
{
    Task<Result<PostalCodeInfo>> ResolveAsync(string codigoPostal, CancellationToken cancellationToken);
}

public sealed record PostalCodeInfo(
    string CodigoPostal,
    string Estado,
    string Municipio,
    IReadOnlyList<Asentamiento> Asentamientos);

public sealed record Asentamiento(string Colonia, string TipoAsentamiento);
