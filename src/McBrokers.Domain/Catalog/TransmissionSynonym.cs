using McBrokers.SharedKernel;

namespace McBrokers.Domain.Catalog;

public sealed class TransmissionSynonym
{
    public Guid Id { get; }
    public string Text { get; }
    public VehicleTransmission Canonical { get; }

    private TransmissionSynonym(Guid id, string text, VehicleTransmission canonical)
    {
        Id = id;
        Text = text;
        Canonical = canonical;
    }

    public static Result<TransmissionSynonym> Create(string text, VehicleTransmission canonical)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<TransmissionSynonym>.Failure("Synonym text must not be empty.");
        }

        return Result<TransmissionSynonym>.Success(new TransmissionSynonym(
            Guid.NewGuid(), text.Trim().ToUpperInvariant(), canonical));
    }
}
