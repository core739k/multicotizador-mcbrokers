using System.Globalization;

namespace McBrokers.Domain.Catalog.Matching;

public static class TokenSetRatio
{
    public static decimal Score(string? a, string? b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);

        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return 0m;
        }

        var intersection = tokensA.Intersect(tokensB).Count();
        var union = tokensA.Union(tokensB).Count();

        if (union == 0) return 0m;

        return Math.Round((decimal)intersection * 100m / union, 2, MidpointRounding.AwayFromZero);
    }

    private static HashSet<string> Tokenize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new HashSet<string>();

        return s
            .ToUpper(CultureInfo.InvariantCulture)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
    }
}
