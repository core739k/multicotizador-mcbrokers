using McBrokers.Domain.Insurers.AxaDxn;

namespace McBrokers.Application.Admin;

public sealed record UpsertAxaDxnConfigCommand(
    Guid InsurerId,
    string Usuario,
    string Password,
    string Tarifa,
    string TarifaPickup,
    int Descuento,
    int DescuentoPickup,
    int MesPolizaDefault,
    string CopsisD4Key,
    string CopsisB);

public sealed record UpsertAxaDxnBusinessCommand(
    Guid InsurerId,
    AxaDxnBusinessName Nombre,
    string? PolizaAutos,
    string? PolizaPickup,
    int Mes);
