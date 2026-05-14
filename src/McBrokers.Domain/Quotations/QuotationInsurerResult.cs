using McBrokers.SharedKernel;

namespace McBrokers.Domain.Quotations;

// Snapshot de los parámetros que el vendedor sobreescribió en la card al
// re-cotizar. Cada campo es nullable — null significa "usa el valor de la
// Quotation". Persiste con el Result para auditoría.
public sealed record QuotationInsurerOverrides(
    Guid? VehicleMasterId,
    ValuationType? Valuation,
    decimal? MaterialDamagesDeductiblePct,
    decimal? RobberyDeductiblePct,
    decimal? MedicalExpensesSumInsured);

public sealed class QuotationInsurerResult
{
    public Guid Id { get; }
    public Guid QuotationId { get; }
    public Guid InsurerId { get; }
    public QuotationInsurerStatus Status { get; }
    public ErrorCategory ErrorCategory { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessageHuman { get; }
    public decimal? PremiumTotal { get; }
    public decimal? PremiumNet { get; }
    public decimal? Tax { get; }
    public decimal? Fees { get; }
    public int LatencyMs { get; }
    public string? ExternalQuoteRef { get; }
    public string? RequestBlobRef { get; }
    public string? ResponseBlobRef { get; }
    public DateTime CreatedAt { get; }
    public int Version { get; }
    public bool IsCurrent { get; private set; }
    // Settable para que EF la materialice como owned entity. Las factories
    // la setean después de invocar el constructor.
    public QuotationInsurerOverrides? Overrides { get; private set; }

    private QuotationInsurerResult(
        Guid id, Guid quotationId, Guid insurerId,
        QuotationInsurerStatus status, ErrorCategory errorCategory,
        string? errorCode, string? errorMessageHuman,
        decimal? premiumTotal, decimal? premiumNet, decimal? tax, decimal? fees,
        int latencyMs, string? externalQuoteRef,
        string? requestBlobRef, string? responseBlobRef, DateTime createdAt,
        int version, bool isCurrent)
    {
        Id = id;
        QuotationId = quotationId;
        InsurerId = insurerId;
        Status = status;
        ErrorCategory = errorCategory;
        ErrorCode = errorCode;
        ErrorMessageHuman = errorMessageHuman;
        PremiumTotal = premiumTotal;
        PremiumNet = premiumNet;
        Tax = tax;
        Fees = fees;
        LatencyMs = latencyMs;
        ExternalQuoteRef = externalQuoteRef;
        RequestBlobRef = requestBlobRef;
        ResponseBlobRef = responseBlobRef;
        CreatedAt = createdAt;
        Version = version;
        IsCurrent = isCurrent;
    }

    public static Result<QuotationInsurerResult> SucceededResult(
        Guid quotationId, Guid insurerId,
        decimal premiumTotal, decimal premiumNet, decimal tax, decimal fees,
        int latencyMs, string externalQuoteRef,
        string? requestBlobRef, string? responseBlobRef, DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<QuotationInsurerResult>.Failure("createdAt must be UTC.");
        }

        if (premiumTotal < 0m) return Result<QuotationInsurerResult>.Failure("premiumTotal must be non-negative.");

        return Result<QuotationInsurerResult>.Success(new QuotationInsurerResult(
            Guid.NewGuid(), quotationId, insurerId,
            QuotationInsurerStatus.Succeeded, ErrorCategory.None,
            errorCode: null, errorMessageHuman: null,
            premiumTotal, premiumNet, tax, fees,
            latencyMs, externalQuoteRef,
            requestBlobRef, responseBlobRef, createdAt,
            version: 1, isCurrent: true));
    }

    public static Result<QuotationInsurerResult> FailedResult(
        Guid quotationId, Guid insurerId,
        QuotationInsurerStatus status, ErrorCategory errorCategory,
        string errorCode, string errorMessageHuman,
        int latencyMs, string? requestBlobRef, string? responseBlobRef, DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<QuotationInsurerResult>.Failure("createdAt must be UTC.");
        }

        if (status == QuotationInsurerStatus.Succeeded)
        {
            return Result<QuotationInsurerResult>.Failure("Use SucceededResult for successful outcomes.");
        }

        return Result<QuotationInsurerResult>.Success(new QuotationInsurerResult(
            Guid.NewGuid(), quotationId, insurerId,
            status, errorCategory,
            errorCode, errorMessageHuman,
            premiumTotal: null, premiumNet: null, tax: null, fees: null,
            latencyMs, externalQuoteRef: null,
            requestBlobRef, responseBlobRef, createdAt,
            version: 1, isCurrent: true));
    }

    // Versiones 2..N son re-cotizaciones disparadas por edición en la card.
    // Llevan overrides obligatorios y conservan el QuotationId/InsurerId del
    // resultado original. La política de "marca anterior como superseded"
    // vive en Quotation.SupersedeAndRecord.
    public static Result<QuotationInsurerResult> SucceededRequoteResult(
        Guid quotationId, Guid insurerId,
        decimal premiumTotal, decimal premiumNet, decimal tax, decimal fees,
        int latencyMs, string externalQuoteRef,
        string? requestBlobRef, string? responseBlobRef, DateTime createdAt,
        int version, QuotationInsurerOverrides overrides)
    {
        if (version < 2) return Result<QuotationInsurerResult>.Failure("Requote version must be 2 or greater.");
        if (createdAt.Kind != DateTimeKind.Utc) return Result<QuotationInsurerResult>.Failure("createdAt must be UTC.");
        if (premiumTotal < 0m) return Result<QuotationInsurerResult>.Failure("premiumTotal must be non-negative.");

        var built = new QuotationInsurerResult(
            Guid.NewGuid(), quotationId, insurerId,
            QuotationInsurerStatus.Succeeded, ErrorCategory.None,
            errorCode: null, errorMessageHuman: null,
            premiumTotal, premiumNet, tax, fees,
            latencyMs, externalQuoteRef,
            requestBlobRef, responseBlobRef, createdAt,
            version, isCurrent: true);
        built.Overrides = overrides;
        return Result<QuotationInsurerResult>.Success(built);
    }

    public static Result<QuotationInsurerResult> FailedRequoteResult(
        Guid quotationId, Guid insurerId,
        QuotationInsurerStatus status, ErrorCategory errorCategory,
        string errorCode, string errorMessageHuman,
        int latencyMs, string? requestBlobRef, string? responseBlobRef, DateTime createdAt,
        int version, QuotationInsurerOverrides overrides)
    {
        if (version < 2) return Result<QuotationInsurerResult>.Failure("Requote version must be 2 or greater.");
        if (createdAt.Kind != DateTimeKind.Utc) return Result<QuotationInsurerResult>.Failure("createdAt must be UTC.");
        if (status == QuotationInsurerStatus.Succeeded)
        {
            return Result<QuotationInsurerResult>.Failure("Use SucceededRequoteResult for successful outcomes.");
        }

        var built = new QuotationInsurerResult(
            Guid.NewGuid(), quotationId, insurerId,
            status, errorCategory,
            errorCode, errorMessageHuman,
            premiumTotal: null, premiumNet: null, tax: null, fees: null,
            latencyMs, externalQuoteRef: null,
            requestBlobRef, responseBlobRef, createdAt,
            version, isCurrent: true);
        built.Overrides = overrides;
        return Result<QuotationInsurerResult>.Success(built);
    }

    public void Supersede() => IsCurrent = false;
}
