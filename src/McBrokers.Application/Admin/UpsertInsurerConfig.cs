using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed class UpsertInsurerConfig
{
    private readonly IInsurerRepository _insurers;
    private readonly IInsurerConfigRepository _configs;
    private readonly IAuditWriter _audit;

    public UpsertInsurerConfig(
        IInsurerRepository insurers,
        IInsurerConfigRepository configs,
        IAuditWriter audit)
    {
        _insurers = insurers;
        _configs = configs;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(UpsertInsurerConfigCommand command, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(command.InsurerId, cancellationToken).ConfigureAwait(false);
        if (insurer is null)
        {
            return Result<Guid>.Failure($"Insurer with id '{command.InsurerId}' not found.");
        }

        var existing = await _configs.GetAsync(command.InsurerId, command.Environment, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var creation = InsurerConfig.Create(
                command.InsurerId,
                command.Environment,
                command.EndpointUrl,
                command.BusinessNumber,
                command.AgentCode,
                command.KeyVaultSecretName,
                command.TimeoutSeconds,
                command.MaxRetries);

            if (!creation.IsSuccess) return Result<Guid>.Failure(creation.Error);

            await _configs.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                action: "InsurerConfig.Create",
                entityType: "InsurerConfig",
                entityId: creation.Value.Id.ToString(),
                payload: new { creation.Value.InsurerId, creation.Value.Environment, creation.Value.EndpointUrl },
                cancellationToken).ConfigureAwait(false);

            return Result<Guid>.Success(creation.Value.Id);
        }

        var update = existing.Update(
            command.EndpointUrl,
            command.BusinessNumber,
            command.AgentCode,
            command.KeyVaultSecretName,
            command.TimeoutSeconds,
            command.MaxRetries);

        if (!update.IsSuccess) return Result<Guid>.Failure(update.Error);

        await _configs.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: "InsurerConfig.Update",
            entityType: "InsurerConfig",
            entityId: existing.Id.ToString(),
            payload: new { existing.InsurerId, existing.Environment, existing.EndpointUrl },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(existing.Id);
    }
}
