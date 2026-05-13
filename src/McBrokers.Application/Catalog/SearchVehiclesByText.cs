using McBrokers.Application.Ports;

namespace McBrokers.Application.Catalog;

// Criterio que viaja al repositorio. Tokens ya separados y no vacíos.
// InsurerIds permisivo: el repo devuelve vehículos con mapping Approved
// para AL MENOS UNA de las aseguradoras listadas.
public sealed record VehicleSearchCriteria(
    int Year,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<Guid> InsurerIds);

// Resultado crudo del repo. AvailableInsurerIds = intersección entre las
// aseguradoras solicitadas y las que tienen mapping Approved para este master.
public sealed record VehicleSearchHit(
    Guid VehicleMasterId,
    int Year,
    string Brand,
    string Model,
    string Version,
    IReadOnlyList<Guid> AvailableInsurerIds);

// Vista proyectada para UI/API.
public sealed record VehicleSearchResultRow(
    Guid VehicleMasterId,
    int Year,
    string Display,
    IReadOnlyList<Guid> AvailableInsurerIds);

public sealed record VehicleSearchResults(IReadOnlyList<VehicleSearchResultRow> Items);

public sealed class SearchVehiclesByText
{
    private static readonly char[] TokenSeparators = new[] { ' ', '\t', '\n', '\r' };

    private readonly ISearchVehiclesByTextRepository _repo;

    public SearchVehiclesByText(ISearchVehiclesByTextRepository repo) => _repo = repo;

    public async Task<VehicleSearchResults> ExecuteAsync(
        int year,
        string? query,
        IReadOnlyList<Guid>? insurerIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new VehicleSearchResults(Array.Empty<VehicleSearchResultRow>());
        }

        var tokens = query
            .Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (tokens.Count == 0)
        {
            return new VehicleSearchResults(Array.Empty<VehicleSearchResultRow>());
        }

        var criteria = new VehicleSearchCriteria(
            year,
            tokens,
            insurerIds ?? Array.Empty<Guid>());

        var hits = await _repo.SearchAsync(criteria, cancellationToken).ConfigureAwait(false);

        var rows = hits
            .Select(h => new VehicleSearchResultRow(
                h.VehicleMasterId,
                h.Year,
                Display: $"{h.Year} {h.Brand} {h.Model} — {h.Version}",
                h.AvailableInsurerIds))
            .ToList();

        return new VehicleSearchResults(rows);
    }
}
