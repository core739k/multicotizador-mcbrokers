using McBrokers.SharedKernel;

namespace McBrokers.Domain.Insurers.AxaDxn;

/// <summary>
/// Configuración específica de AXA DXN. Los campos de negocio (Tarifa, Descuento, etc.)
/// los captura el admin general. La URL del endpoint vive en InsurerTechnicalConfig
/// (sección admin técnico). La Password se persiste cifrada vía IPasswordProtector
/// en el repositorio.
/// </summary>
public sealed class AxaDxnConfig
{
    private const int MaxFieldLength = 100;
    private const int MaxDiscount = 99;

    public Guid Id { get; }
    public Guid InsurerId { get; }
    public string Usuario { get; private set; }
    public string Password { get; private set; }
    public string Tarifa { get; private set; }
    public string TarifaPickup { get; private set; }
    public int Descuento { get; private set; }
    public int DescuentoPickup { get; private set; }
    public int MesPolizaDefault { get; private set; }

    private AxaDxnConfig(
        Guid id, Guid insurerId,
        string usuario, string password,
        string tarifa, string tarifaPickup,
        int descuento, int descuentoPickup,
        int mesPolizaDefault)
    {
        Id = id;
        InsurerId = insurerId;
        Usuario = usuario;
        Password = password;
        Tarifa = tarifa;
        TarifaPickup = tarifaPickup;
        Descuento = descuento;
        DescuentoPickup = descuentoPickup;
        MesPolizaDefault = mesPolizaDefault;
    }

    public static Result<AxaDxnConfig> Create(
        Guid insurerId,
        string usuario, string password,
        string tarifa, string tarifaPickup,
        int descuento, int descuentoPickup,
        int mesPolizaDefault)
    {
        var validation = Validate(usuario, password, tarifa, tarifaPickup,
            descuento, descuentoPickup, mesPolizaDefault);
        if (!validation.IsSuccess) return Result<AxaDxnConfig>.Failure(validation.Error);

        return Result<AxaDxnConfig>.Success(new AxaDxnConfig(
            Guid.NewGuid(), insurerId,
            usuario.Trim(), password,
            tarifa.Trim(), tarifaPickup.Trim(),
            descuento, descuentoPickup, mesPolizaDefault));
    }

    public Result<AxaDxnConfig> Update(
        string usuario, string password,
        string tarifa, string tarifaPickup,
        int descuento, int descuentoPickup,
        int mesPolizaDefault)
    {
        var validation = Validate(usuario, password, tarifa, tarifaPickup,
            descuento, descuentoPickup, mesPolizaDefault);
        if (!validation.IsSuccess) return Result<AxaDxnConfig>.Failure(validation.Error);

        Usuario = usuario.Trim();
        Password = password;
        Tarifa = tarifa.Trim();
        TarifaPickup = tarifaPickup.Trim();
        Descuento = descuento;
        DescuentoPickup = descuentoPickup;
        MesPolizaDefault = mesPolizaDefault;
        return Result<AxaDxnConfig>.Success(this);
    }

    private static Result<bool> Validate(
        string usuario, string password,
        string tarifa, string tarifaPickup,
        int descuento, int descuentoPickup, int mesPolizaDefault)
    {
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > MaxFieldLength)
            return Result<bool>.Failure("Usuario must not be empty and must be ≤ 100 chars.");
        if (string.IsNullOrWhiteSpace(password))
            return Result<bool>.Failure("Password must not be empty.");
        if (string.IsNullOrWhiteSpace(tarifa) || tarifa.Length > MaxFieldLength)
            return Result<bool>.Failure("Tarifa must not be empty and must be ≤ 100 chars.");
        if (string.IsNullOrWhiteSpace(tarifaPickup) || tarifaPickup.Length > MaxFieldLength)
            return Result<bool>.Failure("TarifaPickup must not be empty and must be ≤ 100 chars.");
        if (descuento < 0 || descuento > MaxDiscount)
            return Result<bool>.Failure($"Descuento must be between 0 and {MaxDiscount}.");
        if (descuentoPickup < 0 || descuentoPickup > MaxDiscount)
            return Result<bool>.Failure($"DescuentoPickup must be between 0 and {MaxDiscount}.");
        if (mesPolizaDefault < 1 || mesPolizaDefault > 12)
            return Result<bool>.Failure("MesPolizaDefault must be between 1 and 12.");
        return Result<bool>.Success(true);
    }
}
