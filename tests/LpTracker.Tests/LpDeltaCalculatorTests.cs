using LpTracker.Core.Ranked;

namespace LpTracker.Tests;

public class LpDeltaCalculatorTests
{
    // ---- Compare ----------------------------------------------------------

    [Fact]
    public void One_win_inside_a_division()
    {
        var before = new RankedState("GOLD", "II", 45, Wins: 10, Losses: 8);
        var after = new RankedState("GOLD", "II", 66, Wins: 11, Losses: 8);

        var diff = LpDeltaCalculator.Compare(before, after);

        Assert.Equal(new RankedDiff(GamesPlayed: 1, Wins: 1, Losses: 0, LpChange: 21), diff);
    }

    [Fact]
    public void Promotion_counts_as_a_gain_not_a_loss()
    {
        // Raw LP goes 90 -> 10, which looks like -80. It's really +20.
        var before = new RankedState("GOLD", "I", 90, 20, 15);
        var after = new RankedState("PLATINUM", "IV", 10, 21, 15);

        Assert.Equal(20, LpDeltaCalculator.Compare(before, after)!.LpChange);
    }

    [Fact]
    public void Demotion_counts_as_a_loss_not_a_gain()
    {
        // Raw LP goes 0 -> 82, which looks like +82. It's really -18.
        var before = new RankedState("SILVER", "III", 0, 5, 5);
        var after = new RankedState("SILVER", "IV", 82, 5, 6);

        Assert.Equal(-18, LpDeltaCalculator.Compare(before, after)!.LpChange);
    }

    [Fact]
    public void Promotion_into_master_is_handled()
    {
        var before = new RankedState("DIAMOND", "I", 85, 100, 80);
        var after = new RankedState("MASTER", "I", 5, 101, 80);

        Assert.Equal(20, LpDeltaCalculator.Compare(before, after)!.LpChange);
    }

    [Fact]
    public void No_new_games_returns_null()
    {
        var state = new RankedState("GOLD", "II", 45, 10, 8);

        Assert.Null(LpDeltaCalculator.Compare(state, state));
    }

    [Fact]
    public void Lp_change_without_a_game_returns_null()
    {
        // e.g. a queue dodge penalty: LP drops but no game was played
        var before = new RankedState("GOLD", "II", 45, 10, 8);
        var after = new RankedState("GOLD", "II", 40, 10, 8);

        Assert.Null(LpDeltaCalculator.Compare(before, after));
    }

    [Fact]
    public void New_season_reset_returns_null()
    {
        var lastSeason = new RankedState("GOLD", "I", 50, 120, 110);
        var newSeason = new RankedState("SILVER", "I", 0, 1, 0);

        Assert.Null(LpDeltaCalculator.Compare(lastSeason, newSeason));
    }

    // ---- SplitAcrossGames -------------------------------------------------

    [Fact]
    public void Single_game_gets_the_exact_lp()
    {
        var games = LpDeltaCalculator.SplitAcrossGames(21, new[] { true });

        Assert.Equal(new GameLp(true, 21, IsEstimated: false), Assert.Single(games));
    }

    [Fact]
    public void Several_games_split_evenly_and_are_marked_estimated()
    {
        var games = LpDeltaCalculator.SplitAcrossGames(40, new[] { true, true });

        Assert.Equal(new[] { 20, 20 }, games.Select(g => g.LpDelta));
        Assert.All(games, g => Assert.True(g.IsEstimated));
    }

    [Theory]
    [InlineData(41, 3)]
    [InlineData(-37, 4)]
    [InlineData(2, 3)]
    [InlineData(0, 2)]
    public void Split_always_adds_back_up_to_the_total(int total, int gameCount)
    {
        var results = Enumerable.Repeat(true, gameCount).ToArray();

        var games = LpDeltaCalculator.SplitAcrossGames(total, results);

        Assert.Equal(gameCount, games.Count);
        Assert.Equal(total, games.Sum(g => g.LpDelta));
    }

    [Fact]
    public void Split_keeps_each_games_win_or_loss()
    {
        var games = LpDeltaCalculator.SplitAcrossGames(3, new[] { true, false });

        Assert.Equal(new[] { true, false }, games.Select(g => g.Won));
    }

    [Fact]
    public void No_games_returns_empty()
    {
        Assert.Empty(LpDeltaCalculator.SplitAcrossGames(10, Array.Empty<bool>()));
    }
}
