using McBrokers.Domain.Catalog.Matching;

namespace McBrokers.Domain.Tests.Catalog;

public class TextNormalizerTests
{
    private static readonly IReadOnlyDictionary<string, string> Synonyms = new Dictionary<string, string>
    {
        ["STD"] = "ESTANDAR",
        ["AUT"] = "AUTOMATICO",
        ["C/A"] = "AC",
        ["4CIL"] = "4 CILINDROS",
        ["CIL"] = "CILINDROS",
    };

    private static TextNormalizer Build() => new(Synonyms);

    [Theory]
    [InlineData("aveo lt", "AVEO LT")]
    [InlineData("Aveo  LT", "AVEO LT")]
    [InlineData("  Aveo LT  ", "AVEO LT")]
    public void Uppercases_and_collapses_whitespace(string input, string expected)
    {
        Build().Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("AVEO, LT", "AVEO LT")]
    [InlineData("AVEO. LT - PLUS", "AVEO LT PLUS")]
    [InlineData("AVEO/LT(PLUS)", "AVEO LT PLUS")]
    public void Strips_punctuation(string input, string expected)
    {
        Build().Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("CIVIC STD", "CIVIC ESTANDAR")]
    [InlineData("CIVIC AUT", "CIVIC AUTOMATICO")]
    [InlineData("CIVIC 4CIL", "CIVIC 4 CILINDROS")]
    [InlineData("CIVIC C/A", "CIVIC AC")]
    public void Applies_synonyms(string input, string expected)
    {
        Build().Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Synonym_replacement_is_token_aware()
    {
        // "STD" matches "STD" as a token but should not match within "STDX" or "STDIO"
        Build().Normalize("STDIO STD").Should().Be("STDIO ESTANDAR");
    }

    [Fact]
    public void Empty_or_null_input_returns_empty()
    {
        Build().Normalize(null).Should().Be(string.Empty);
        Build().Normalize("").Should().Be(string.Empty);
        Build().Normalize("   ").Should().Be(string.Empty);
    }

    [Fact]
    public void Multiple_synonyms_in_same_string_all_replaced()
    {
        Build().Normalize("STD AUT 4CIL").Should().Be("ESTANDAR AUTOMATICO 4 CILINDROS");
    }

    [Fact]
    public void Real_world_example_normalizes_predictably()
    {
        Build().Normalize("Aveo LT, 4cil, std").Should().Be("AVEO LT 4 CILINDROS ESTANDAR");
    }
}
