using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed record UpsertInsurerPackageMappingCommand(
    Guid InsurerId,
    PackageCode InternalPackage,
    string ExternalCode,
    string? Description);

public sealed class UpsertInsurerPackageMapping
{
    private readonly IInsurerRepository _insurers;
    private readonly IInsurerPackageMappingRepository _mappings;
    private readonly IAuditWriter _audit;

    public UpsertInsurerPackageMapping(
        IInsurerRepository insurers,
        IInsurerPackageMappingRepository mappings,
        IAuditWriter audit)
    {
        _insurers = insurers;
        _mappings = mappings;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        UpsertInsurerPackageMappingCommand command, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(command.InsurerId, cancellationToken).ConfigureAwait(false);
        if (insurer is null)
        {
            return Result<Guid>.Failure($"Insurer with id '{command.InsurerId}' not found.");
        }

        var existing = await _mappings
            .GetAsync(command.InsurerId, command.InternalPackage, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var creation = InsurerPackageMapping.Create(
                command.InsurerId, command.InternalPackage, command.ExternalCode, command.Description);
            if (!creation.IsSuccess) return Result<Guid>.Failure(creation.Error);

            await _mappings.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                action: "InsurerPackageMapping.Create",
                entityType: "InsurerPackageMapping",
                entityId: creation.Value.Id.ToString(),
                payload: new { command.InsurerId, command.InternalPackage, command.ExternalCode },
                cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(creation.Value.Id);
        }

        var update = existing.Update(command.ExternalCode, command.Description);
        if (!update.IsSuccess) return Result<Guid>.Failure(update.Error);

        await _mappings.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: "InsurerPackageMapping.Update",
            entityType: "InsurerPackageMapping",
            entityId: existing.Id.ToString(),
            payload: new { command.InsurerId, command.InternalPackage, command.ExternalCode },
            cancellationToken).ConfigureAwait(false);
        return Result<Guid>.Success(existing.Id);
    }
}
