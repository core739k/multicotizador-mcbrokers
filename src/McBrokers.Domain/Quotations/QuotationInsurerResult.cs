using McBrokers.SharedKernel;

namespace McBrokers.Domain.Quotations;

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

    private QuotationInsurerResult(
        Guid id, Guid quotationId, Guid insurerId,
        QuotationInsurerStatus status, ErrorCategory errorCategory,
        string? errorCode, string? errorMessageHuman,
        decimal? premiumTotal, decimal? premiumNet, decimal? tax, decimal? fees,
        int latencyMs, string? externalQuoteRef,
        string? requestBlobRef, string? responseBlobRef, DateTime createdAt)
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
            requestBlobRef, responseBlobRef, createdAt));
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
            requestBlobRef, responseBlobRef, createdAt));
    }
}
