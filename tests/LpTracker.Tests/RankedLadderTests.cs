using LpTracker.Core.Ranked;

namespace LpTracker.Tests;

public class RankedLadderTests
{
    [Theory]
    [InlineData("IRON", "IV", 0, 0)]
    [InlineData("IRON", "III", 0, 100)]
    [InlineData("IRON", "I", 99, 399)]
    [InlineData("BRONZE", "IV", 0, 400)]
    [InlineData("GOLD", "II", 45, 1445)]      // 3 tiers * 400 + 2 divisions * 100 + 45
    [InlineData("DIAMOND", "I", 75, 2775)]
    [InlineData("MASTER", "I", 0, 2800)]
    [InlineData("GRANDMASTER", "I", 350, 3150)]
    [InlineData("CHALLENGER", "I", 1200, 4000)]
    public void Converts_rank_to_ladder_points(string tier, string division, int lp, int expected)
    {
        Assert.Equal(expected, RankedLadder.ToLadderPoints(tier, division, lp));
    }

    [Fact]
    public void Tier_and_division_are_case_insensitive()
    {
        Assert.Equal(RankedLadder.ToLadderPoints("GOLD", "II", 10), RankedLadder.ToLadderPoints("gold", "ii", 10));
    }

    [Fact]
    public void Unknown_tier_throws()
    {
        Assert.Throws<ArgumentException>(() => RankedLadder.ToLadderPoints("WOOD", "IV", 0));
    }
}
