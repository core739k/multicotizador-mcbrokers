using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Ports;

public interface IInsurerPackageMappingRepository
{
    /// <summary>
    /// Devuelve el código externo (CVE_PAQUETE para GNP, etc.) para un paquete interno y aseguradora.
    /// </summary>
    Task<string?> GetExternalCodeAsync(
        Guid insurerId, PackageCode internalPackage, CancellationToken cancellationToken);
}
