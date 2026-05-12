using McBrokers.SharedKernel;

namespace McBrokers.Domain.Insurers.AxaDxn;

/// <summary>
/// Negocio bajo el que se opera con AXA DXN (BIMBO/STRM/MCB/CAJA/CTBR).
/// PolizaAutos y PolizaPickup son nullable porque solo STRM tiene valores reales hoy.
/// El vendedor selecciona el negocio al cotizar; las pólizas resultantes se mandan
/// a AXA en el campo numeroPoliza.
/// </summary>
public sealed class AxaDxnBusiness
{
    private const int MaxPolicyLength = 50;

    public Guid Id { get; }
    public Guid AxaDxnConfigId { get; }
    public AxaDxnBusinessName Nombre { get; }
    public string? PolizaAutos { get; private set; }
    public string? PolizaPickup { get; private set; }
    public int Mes { get; private set; }

    private AxaDxnBusiness(
        Guid id, Guid axaDxnConfigId, AxaDxnBusinessName nombre,
        string? polizaAutos, string? polizaPickup, int mes)
    {
        Id = id;
        AxaDxnConfigId = axaDxnConfigId;
        Nombre = nombre;
        PolizaAutos = polizaAutos;
        PolizaPickup = polizaPickup;
        Mes = mes;
    }

    public static Result<AxaDxnBusiness> Create(
        Guid axaDxnConfigId,
        AxaDxnBusinessName nombre,
        string? polizaAutos,
        string? polizaPickup,
        int mes)
    {
        var validation = Validate(polizaAutos, polizaPickup, mes);
        if (!validation.IsSuccess) return Result<AxaDxnBusiness>.Failure(validation.Error);

        return Result<AxaDxnBusiness>.Success(new AxaDxnBusiness(
            Guid.NewGuid(), axaDxnConfigId, nombre,
            Trim(polizaAutos), Trim(polizaPickup), mes));
    }

    public Result<AxaDxnBusiness> Update(
        string? polizaAutos,
        string? polizaPickup,
        int mes)
    {
        var validation = Validate(polizaAutos, polizaPickup, mes);
        if (!validation.IsSuccess) return Result<AxaDxnBusiness>.Failure(validation.Error);

        PolizaAutos = Trim(polizaAutos);
        PolizaPickup = Trim(polizaPickup);
        Mes = mes;
        return Result<AxaDxnBusiness>.Success(this);
    }

    private static Result<bool> Validate(string? polizaAutos, string? polizaPickup, int mes)
    {
        if (polizaAutos is not null && polizaAutos.Length > MaxPolicyLength)
            return Result<bool>.Failure($"PolizaAutos must be ≤ {MaxPolicyLength} chars.");
        if (polizaPickup is not null && polizaPickup.Length > MaxPolicyLength)
            return Result<bool>.Failure($"PolizaPickup must be ≤ {MaxPolicyLength} chars.");
        if (mes < 1 || mes > 12)
            return Result<bool>.Failure("Mes must be between 1 and 12.");
        return Result<bool>.Success(true);
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
