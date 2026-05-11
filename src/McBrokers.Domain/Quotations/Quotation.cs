using System.Text.RegularExpressions;
using McBrokers.SharedKernel;

namespace McBrokers.Domain.Quotations;

public sealed class Quotation
{
    private static readonly Regex PostalCodePattern = new(@"^\d{5}$", RegexOptions.Compiled);

    public Guid Id { get; }
    public string CorrelationId { get; }
    public Guid AgentId { get; }
    public Guid VehicleMasterId { get; }
    public PackageCode Package { get; }
    public PaymentMode PaymentMode { get; }
    public ValuationType ValuationType { get; }
    public decimal SumInsured { get; }
    public string PostalCode { get; }
    public string CustomerSnapshotJson { get; }
    public QuotationStatus Status { get; private set; }
    public int ExpectedResultsCount { get; private set; }
    public DateTime CreatedAt { get; }

    private readonly List<QuotationInsurerResult> _results = new();
    public IReadOnlyList<QuotationInsurerResult> Results => _results;

    private Quotation(
        Guid id, string correlationId, Guid agentId, Guid vehicleMasterId,
        PackageCode package, PaymentMode paymentMode, ValuationType valuationType,
        decimal sumInsured, string postalCode, string customerSnapshotJson,
        DateTime createdAt)
    {
        Id = id;
        CorrelationId = correlationId;
        AgentId = agentId;
        VehicleMasterId = vehicleMasterId;
        Package = package;
        PaymentMode = paymentMode;
        ValuationType = valuationType;
        SumInsured = sumInsured;
        PostalCode = postalCode;
        CustomerSnapshotJson = customerSnapshotJson;
        Status = QuotationStatus.Pending;
        CreatedAt = createdAt;
    }

    public static Result<Quotation> Create(
        Guid agentId, string correlationId, Guid vehicleMasterId,
        PackageCode package, PaymentMode paymentMode, ValuationType valuationType,
        decimal sumInsured, string postalCode, string customerSnapshotJson,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Result<Quotation>.Failure("CorrelationId must not be empty.");
        }

        if (sumInsured <= 0m)
        {
            return Result<Quotation>.Failure("SumInsured must be positive.");
        }

        if (string.IsNullOrWhiteSpace(postalCode) || !PostalCodePattern.IsMatch(postalCode))
        {
            return Result<Quotation>.Failure("PostalCode must be a 5-digit Mexican CP.");
        }

        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<Quotation>.Failure("createdAt must be UTC.");
        }

        return Result<Quotation>.Success(new Quotation(
            Guid.NewGuid(), correlationId, agentId, vehicleMasterId,
            package, paymentMode, valuationType,
            sumInsured, postalCode,
            customerSnapshotJson ?? "{}",
            createdAt));
    }

    public void ExpectResultsFrom(int insurerCount)
    {
        if (insurerCount < 1)
        {
            throw new ArgumentException("Expected results count must be at least 1.", nameof(insurerCount));
        }
        ExpectedResultsCount = insurerCount;
    }

    public void RecordResult(QuotationInsurerResult result)
    {
        if (result.QuotationId != Id)
        {
            throw new ArgumentException(
                $"Result belongs to quotation '{result.QuotationId}', not this one ('{Id}').", nameof(result));
        }
        _results.Add(result);
        RecomputeStatus();
    }

    /// <summary>
    /// Llamado por el repositorio al cargar desde BD. Reconstruye los results en memoria
    /// y recomputa el Status para mantener invariantes.
    /// </summary>
    public void Rehydrate(IEnumerable<QuotationInsurerResult> persistedResults)
    {
        _results.Clear();
        _results.AddRange(persistedResults);
        RecomputeStatus();
    }

    private void RecomputeStatus()
    {
        if (ExpectedResultsCount == 0 || _results.Count == 0)
        {
            Status = QuotationStatus.Pending;
            return;
        }

        if (_results.Count < ExpectedResultsCount)
        {
            Status = QuotationStatus.Partial;
            return;
        }

        var anySucceeded = _results.Any(r => r.Status == QuotationInsurerStatus.Succeeded);
        Status = anySucceeded ? QuotationStatus.Completed : QuotationStatus.Failed;
    }
}
