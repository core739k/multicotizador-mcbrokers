using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed class UpsertAxaDxnBusiness
{
    private readonly IAxaDxnConfigRepository _repo;
    private readonly IAuditWriter _audit;

    public UpsertAxaDxnBusiness(IAxaDxnConfigRepository repo, IAuditWriter audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        UpsertAxaDxnBusinessCommand command, CancellationToken cancellationToken)
    {
        var config = await _repo.GetByInsurerIdAsync(command.InsurerId, cancellationToken).ConfigureAwait(false);
        if (config is null)
            return Result<Guid>.Failure(
                "AxaDxnConfig not found for this insurer. Save the main configuration first.");

        var existing = await _repo.GetBusinessAsync(config.Config.Id, command.Nombre, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var creation = AxaDxnBusiness.Create(
                config.Config.Id, command.Nombre,
                command.PolizaAutos, command.PolizaPickup, command.Mes);
            if (!creation.IsSuccess) return Result<Guid>.Failure(creation.Error);

            await _repo.AddBusinessAsync(creation.Value, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                "AxaDxnBusiness.Create", "AxaDxnBusiness", creation.Value.Id.ToString(),
                new { creation.Value.AxaDxnConfigId, creation.Value.Nombre, creation.Value.PolizaAutos, creation.Value.PolizaPickup, creation.Value.Mes },
                cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(creation.Value.Id);
        }

        var update = existing.Update(command.PolizaAutos, command.PolizaPickup, command.Mes);
        if (!update.IsSuccess) return Result<Guid>.Failure(update.Error);

        await _repo.UpdateBusinessAsync(existing, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            "AxaDxnBusiness.Update", "AxaDxnBusiness", existing.Id.ToString(),
            new { existing.AxaDxnConfigId, existing.Nombre, existing.PolizaAutos, existing.PolizaPickup, existing.Mes },
            cancellationToken).ConfigureAwait(false);
        return Result<Guid>.Success(existing.Id);
    }
}
