using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers.AxaDxn;

namespace McBrokers.Application.Admin;

public sealed record AxaDxnConfigView(
    Guid Id,
    Guid InsurerId,
    string Usuario,
    string Tarifa,
    string TarifaPickup,
    int Descuento,
    int DescuentoPickup,
    int MesPolizaDefault,
    IReadOnlyList<AxaDxnBusinessView> Businesses)
{
    // Password se omite del view — nunca se devuelve al cliente.
    public static AxaDxnConfigView From(AxaDxnConfigWithBusinesses snapshot) =>
        new(snapshot.Config.Id, snapshot.Config.InsurerId,
            snapshot.Config.Usuario, snapshot.Config.Tarifa, snapshot.Config.TarifaPickup,
            snapshot.Config.Descuento, snapshot.Config.DescuentoPickup, snapshot.Config.MesPolizaDefault,
            snapshot.Businesses.Select(AxaDxnBusinessView.From).ToList());
}

public sealed record AxaDxnBusinessView(
    Guid Id,
    AxaDxnBusinessName Nombre,
    string? PolizaAutos,
    string? PolizaPickup,
    int Mes)
{
    public static AxaDxnBusinessView From(AxaDxnBusiness b) =>
        new(b.Id, b.Nombre, b.PolizaAutos, b.PolizaPickup, b.Mes);
}

public sealed class GetAxaDxnConfig
{
    private readonly IAxaDxnConfigRepository _repo;

    public GetAxaDxnConfig(IAxaDxnConfigRepository repo) => _repo = repo;

    public async Task<AxaDxnConfigView?> ExecuteAsync(Guid insurerId, CancellationToken cancellationToken)
    {
        var snapshot = await _repo.GetByInsurerIdAsync(insurerId, cancellationToken).ConfigureAwait(false);
        return snapshot is null ? null : AxaDxnConfigView.From(snapshot);
    }
}
