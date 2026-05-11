using McBrokers.Domain.Quotations;
using McBrokers.SharedKernel;

namespace McBrokers.Domain.Insurers;

/// <summary>
/// Mapeo de paquete interno (AMPLIA/LIMITADA/RC) al código externo que espera cada aseguradora
/// (ej. GNP CVE_PAQUETE, Quálitas paquete numérico, etc.). Administrable.
/// </summary>
public sealed class InsurerPackageMapping
{
    public Guid Id { get; }
    public Guid InsurerId { get; }
    public PackageCode InternalPackage { get; }
    public string ExternalCode { get; private set; }
    public string? Description { get; private set; }

    private InsurerPackageMapping(
        Guid id, Guid insurerId, PackageCode internalPackage, string externalCode, string? description)
    {
        Id = id;
        InsurerId = insurerId;
        InternalPackage = internalPackage;
        ExternalCode = externalCode;
        Description = description;
    }

    public static Result<InsurerPackageMapping> Create(
        Guid insurerId, PackageCode internalPackage, string externalCode, string? description)
    {
        if (string.IsNullOrWhiteSpace(externalCode))
        {
            return Result<InsurerPackageMapping>.Failure("ExternalCode must not be empty.");
        }

        return Result<InsurerPackageMapping>.Success(new InsurerPackageMapping(
            Guid.NewGuid(), insurerId, internalPackage,
            externalCode.Trim(), description?.Trim()));
    }

    public Result<InsurerPackageMapping> Update(string externalCode, string? description)
    {
        if (string.IsNullOrWhiteSpace(externalCode))
        {
            return Result<InsurerPackageMapping>.Failure("ExternalCode must not be empty.");
        }

        ExternalCode = externalCode.Trim();
        Description = description?.Trim();
        return Result<InsurerPackageMapping>.Success(this);
    }
}
