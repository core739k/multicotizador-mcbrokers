using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed class CreateInsurer
{
    private readonly IInsurerRepository _insurers;
    private readonly IAuditWriter _audit;

    public CreateInsurer(IInsurerRepository insurers, IAuditWriter audit)
    {
        _insurers = insurers;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateInsurerCommand command, CancellationToken cancellationToken)
    {
        var existing = await _insurers.GetByCodeAsync(command.Code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<Guid>.Failure($"Insurer with code '{command.Code}' already exists.");
        }

        var insurer = Insurer.Create(command.Code, command.Name, command.DisplayOrder);
        if (!insurer.IsSuccess)
        {
            return Result<Guid>.Failure(insurer.Error);
        }

        await _insurers.AddAsync(insurer.Value, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: "Insurer.Create",
            entityType: "Insurer",
            entityId: insurer.Value.Id.ToString(),
            payload: new { insurer.Value.Code, insurer.Value.Name, insurer.Value.DisplayOrder },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(insurer.Value.Id);
    }
}
