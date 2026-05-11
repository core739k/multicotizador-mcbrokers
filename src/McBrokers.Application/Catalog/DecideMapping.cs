using McBrokers.Application.Ports;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Catalog;

public enum Decision { Approve, Reject }

public sealed record DecideMappingCommand(Guid MappingId, Decision Decision);

public sealed class DecideMapping
{
    private readonly IVehicleInsurerMappingRepository _mappings;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly ICurrentAgentProvider _currentAgent;

    public DecideMapping(
        IVehicleInsurerMappingRepository mappings,
        IAuditWriter audit,
        IClock clock,
        ICurrentAgentProvider currentAgent)
    {
        _mappings = mappings;
        _audit = audit;
        _clock = clock;
        _currentAgent = currentAgent;
    }

    public async Task<Result<Guid>> ExecuteAsync(DecideMappingCommand command, CancellationToken cancellationToken)
    {
        var mapping = await _mappings.GetByIdAsync(command.MappingId, cancellationToken).ConfigureAwait(false);
        if (mapping is null)
        {
            return Result<Guid>.Failure($"Mapping with id '{command.MappingId}' not found.");
        }

        if (command.Decision == Decision.Approve)
        {
            mapping.Approve(_currentAgent.AgentId, _clock.UtcNow);
        }
        else
        {
            mapping.Reject(_currentAgent.AgentId, _clock.UtcNow);
        }

        await _mappings.UpdateAsync(mapping, cancellationToken).ConfigureAwait(false);

        var action = command.Decision == Decision.Approve ? "CatalogMapping.Approve" : "CatalogMapping.Reject";
        await _audit.WriteAsync(
            action, "VehicleInsurerMapping", mapping.Id.ToString(),
            new { mapping.VehicleMasterId, mapping.InsurerId, mapping.ConfidenceScore },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(mapping.Id);
    }
}
