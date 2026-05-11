using McBrokers.Domain.Catalog.Matching;

namespace McBrokers.Domain.Tests.Catalog;

public class TokenSetRatioTests
{
    [Fact]
    public void Identical_strings_score_100()
    {
        TokenSetRatio.Score("AVEO LT", "AVEO LT").Should().Be(100m);
    }

    [Fact]
    public void Token_order_does_not_matter()
    {
        TokenSetRatio.Score("AVEO LT PLUS PACK", "PLUS PACK AVEO LT").Should().Be(100m);
    }

    [Fact]
    public void Duplicate_tokens_do_not_inflate_score()
    {
        TokenSetRatio.Score("AVEO AVEO LT", "AVEO LT").Should().Be(100m);
    }

    [Fact]
    public void Partial_overlap_scores_below_100()
    {
        // tokens A: {AVEO, LT} ; B: {AVEO, LT, PLUS}
        // intersection 2, union 3 → 66.66
        var score = TokenSetRatio.Score("AVEO LT", "AVEO LT PLUS");
        score.Should().BeInRange(66m, 67m);
    }

    [Fact]
    public void Disjoint_strings_score_0()
    {
        TokenSetRatio.Score("AVEO LT", "CIVIC EX").Should().Be(0m);
    }

    [Fact]
    public void Empty_versus_non_empty_scores_0()
    {
        TokenSetRatio.Score("", "AVEO LT").Should().Be(0m);
        TokenSetRatio.Score("AVEO LT", "").Should().Be(0m);
    }

    [Fact]
    public void Both_empty_scores_0()
    {
        TokenSetRatio.Score("", "").Should().Be(0m);
    }

    [Fact]
    public void Whitespace_only_treated_as_empty()
    {
        TokenSetRatio.Score("   ", "AVEO").Should().Be(0m);
    }

    [Fact]
    public void Case_does_not_matter()
    {
        TokenSetRatio.Score("aveo lt", "AVEO LT").Should().Be(100m);
    }

    [Fact]
    public void Threshold_real_world_examples()
    {
        // Qua: "AVEO LT" vs AXA: "AVEO LT" → 100 (auto-approve)
        TokenSetRatio.Score("AVEO LT", "AVEO LT").Should().BeGreaterThanOrEqualTo(95m);

        // Qua: "AVEO LT PLUS PACK" vs AXA: "AVEO LT" → below threshold (3 vs 2 tokens, 1 missing)
        TokenSetRatio.Score("AVEO LT PLUS PACK", "AVEO LT").Should().BeLessThan(95m);
    }
}
