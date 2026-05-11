using McBrokers.Application.Ports;
using McBrokers.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

/// <summary>
/// F3 placeholder: por ahora resuelve a partir de una tabla simple Insurer-PackageMapping.
/// Cuando se administre por UI (Fase 1.5) se cargará desde una entidad propia con CRUD.
/// </summary>
public class InsurerPackageMappingRepository : IInsurerPackageMappingRepository
{
    private readonly AppDbContext _db;
    public InsurerPackageMappingRepository(AppDbContext db) => _db = db;

    public async Task<string?> GetExternalCodeAsync(
        Guid insurerId, PackageCode internalPackage, CancellationToken cancellationToken)
    {
        // Hay una tabla legada en F1: nada todavía. Para F3 devolvemos null para que
        // ProcessQuotation use string.Empty (lo que GNP rechazará con un error claro).
        // El admin debe poblar configuracion_claves_gnp (futura UI / seed).
        await Task.CompletedTask;
        return null;
    }
}
