using System.Globalization;
using System.Text.RegularExpressions;

namespace McBrokers.Domain.Catalog.Matching;

public sealed class TextNormalizer
{
    private static readonly Regex Punctuation = new(@"[\p{P}\p{S}]", RegexOptions.Compiled);
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, string> _synonyms;

    public TextNormalizer(IReadOnlyDictionary<string, string>? synonyms = null)
    {
        _synonyms = synonyms ?? new Dictionary<string, string>();
    }

    public string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var s = input.ToUpper(CultureInfo.InvariantCulture);

        // Apply synonyms BEFORE stripping punctuation so that multi-char keys like "C/A"
        // can match. Token-aware: only replace whole tokens (bounded by non-letter-or-digit
        // chars at start AND end), so "STDIO" is left intact while "STD" is replaced.
        foreach (var (key, replacement) in _synonyms)
        {
            var upperKey = key.ToUpper(CultureInfo.InvariantCulture);
            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(upperKey)}(?![\p{{L}}\p{{N}}])";
            s = Regex.Replace(s, pattern, replacement.ToUpper(CultureInfo.InvariantCulture));
        }

        s = Punctuation.Replace(s, " ");
        s = MultiWhitespace.Replace(s, " ").Trim();
        return s;
    }
}
