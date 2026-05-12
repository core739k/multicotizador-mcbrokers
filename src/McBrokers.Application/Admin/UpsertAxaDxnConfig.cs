using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed class UpsertAxaDxnConfig
{
    private readonly IInsurerRepository _insurers;
    private readonly IAxaDxnConfigRepository _repo;
    private readonly IAuditWriter _audit;

    public UpsertAxaDxnConfig(
        IInsurerRepository insurers,
        IAxaDxnConfigRepository repo,
        IAuditWriter audit)
    {
        _insurers = insurers;
        _repo = repo;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        UpsertAxaDxnConfigCommand command, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(command.InsurerId, cancellationToken).ConfigureAwait(false);
        if (insurer is null)
            return Result<Guid>.Failure($"Insurer with id '{command.InsurerId}' not found.");

        var existing = await _repo.GetByInsurerIdAsync(command.InsurerId, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var creation = AxaDxnConfig.Create(
                command.InsurerId, command.Usuario, command.Password,
                command.Tarifa, command.TarifaPickup,
                command.Descuento, command.DescuentoPickup, command.MesPolizaDefault,
                command.CopsisD4Key, command.CopsisB);
            if (!creation.IsSuccess) return Result<Guid>.Failure(creation.Error);

            await _repo.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                "AxaDxnConfig.Create", "AxaDxnConfig", creation.Value.Id.ToString(),
                new { creation.Value.InsurerId, creation.Value.Usuario, creation.Value.Tarifa, creation.Value.MesPolizaDefault },
                cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(creation.Value.Id);
        }

        var update = existing.Config.Update(
            command.Usuario, command.Password,
            command.Tarifa, command.TarifaPickup,
            command.Descuento, command.DescuentoPickup, command.MesPolizaDefault,
            command.CopsisD4Key, command.CopsisB);
        if (!update.IsSuccess) return Result<Guid>.Failure(update.Error);

        await _repo.UpdateAsync(existing.Config, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            "AxaDxnConfig.Update", "AxaDxnConfig", existing.Config.Id.ToString(),
            new { existing.Config.InsurerId, existing.Config.Usuario, existing.Config.Tarifa, existing.Config.MesPolizaDefault },
            cancellationToken).ConfigureAwait(false);
        return Result<Guid>.Success(existing.Config.Id);
    }
}
