namespace McBrokers.Insurers.Abstractions;

public abstract record InsurerQuoteOutcome
{
    public sealed record Success(InsurerQuoteResponse Response) : InsurerQuoteOutcome;

    public sealed record Failure(InsurerErrorResponse Error) : InsurerQuoteOutcome;
}
