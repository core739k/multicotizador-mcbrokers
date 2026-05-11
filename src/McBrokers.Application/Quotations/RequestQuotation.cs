using McBrokers.Application.Ports;
using McBrokers.Domain.Quotations;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Quotations;

public sealed record RequestQuotationCommand(
    Guid VehicleMasterId,
    PackageCode Package,
    PaymentMode PaymentMode,
    ValuationType ValuationType,
    decimal SumInsured,
    string PostalCode,
    string CustomerSnapshotJson);

public sealed record RequestQuotationResult(Guid QuotationId, string CorrelationId);

public sealed class RequestQuotation
{
    private readonly IQuotationRepository _quotations;
    private readonly IQuotationQueue _queue;
    private readonly IClock _clock;
    private readonly ICurrentAgentProvider _currentAgent;
    private readonly IAuditWriter _audit;

    public RequestQuotation(
        IQuotationRepository quotations,
        IQuotationQueue queue,
        IClock clock,
        ICurrentAgentProvider currentAgent,
        IAuditWriter audit)
    {
        _quotations = quotations;
        _queue = queue;
        _clock = clock;
        _currentAgent = currentAgent;
        _audit = audit;
    }

    public async Task<Result<RequestQuotationResult>> ExecuteAsync(
        RequestQuotationCommand command, string? correlationId, CancellationToken cancellationToken)
    {
        var corr = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("n") : correlationId;

        var creation = Quotation.Create(
            agentId: _currentAgent.AgentId,
            correlationId: corr,
            vehicleMasterId: command.VehicleMasterId,
            package: command.Package,
            paymentMode: command.PaymentMode,
            valuationType: command.ValuationType,
            sumInsured: command.SumInsured,
            postalCode: command.PostalCode,
            customerSnapshotJson: command.CustomerSnapshotJson,
            createdAt: _clock.UtcNow);

        if (!creation.IsSuccess)
        {
            return Result<RequestQuotationResult>.Failure(creation.Error);
        }

        var quotation = creation.Value;

        // F3: only GNP enabled. Mark expected results = 1 so the worker can decide when complete.
        quotation.ExpectResultsFrom(1);

        await _quotations.AddAsync(quotation, cancellationToken).ConfigureAwait(false);

        await _queue.EnqueueAsync(
            new QuotationWorkItem(quotation.Id, quotation.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        await _audit.WriteAsync(
            action: "Quotation.Request",
            entityType: "Quotation",
            entityId: quotation.Id.ToString(),
            payload: new { quotation.VehicleMasterId, quotation.Package, quotation.SumInsured, quotation.PostalCode },
            cancellationToken).ConfigureAwait(false);

        return Result<RequestQuotationResult>.Success(
            new RequestQuotationResult(quotation.Id, quotation.CorrelationId));
    }
}
