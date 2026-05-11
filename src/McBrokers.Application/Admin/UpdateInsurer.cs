using McBrokers.Application.Ports;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed class UpdateInsurer
{
    private readonly IInsurerRepository _insurers;
    private readonly IAuditWriter _audit;

    public UpdateInsurer(IInsurerRepository insurers, IAuditWriter audit)
    {
        _insurers = insurers;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(UpdateInsurerCommand command, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (insurer is null)
        {
            return Result<Guid>.Failure($"Insurer with id '{command.Id}' not found.");
        }

        var rename = insurer.Rename(command.Name);
        if (!rename.IsSuccess) return Result<Guid>.Failure(rename.Error);

        var order = insurer.SetDisplayOrder(command.DisplayOrder);
        if (!order.IsSuccess) return Result<Guid>.Failure(order.Error);

        var logo = insurer.SetLogoUrl(command.LogoUrl);
        if (!logo.IsSuccess) return Result<Guid>.Failure(logo.Error);

        if (command.IsEnabled) insurer.Enable();
        else insurer.Disable();

        await _insurers.UpdateAsync(insurer, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: "Insurer.Update",
            entityType: "Insurer",
            entityId: insurer.Id.ToString(),
            payload: new
            {
                insurer.Name,
                insurer.DisplayOrder,
                insurer.IsEnabled,
                insurer.LogoUrl,
            },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(insurer.Id);
    }
}
