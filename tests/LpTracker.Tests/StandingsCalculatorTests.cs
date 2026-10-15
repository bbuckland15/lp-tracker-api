using LpTracker.Core.Standings;

namespace LpTracker.Tests;

/// <summary>Unit tests for ranking logic. No database needed, so these run in milliseconds.</summary>
public class StandingsCalculatorTests
{
    [Fact]
    public void Highest_lp_is_ranked_first()
    {
        var totals = new[]
        {
            new PlayerTotals(1, "Ben", 40, 3, 1),
            new PlayerTotals(2, "Alex", 75, 4, 0),
            new PlayerTotals(3, "Jordan", -10, 1, 2),
        };

        var standings = StandingsCalculator.Rank(totals);

        Assert.Equal(new[] { "Alex", "Ben", "Jordan" }, standings.Select(s => s.Name));
        Assert.Equal(new[] { 1, 2, 3 }, standings.Select(s => s.Position));
    }

    [Fact]
    public void Tied_players_share_a_position_and_the_next_position_is_skipped()
    {
        // Same behavior as SQL's RANK(): 1, 2, 2, 4
        var totals = new[]
        {
            new PlayerTotals(1, "Ben", 50, 0, 0),
            new PlayerTotals(2, "Alex", 30, 0, 0),
            new PlayerTotals(3, "Jordan", 30, 0, 0),
            new PlayerTotals(4, "Sam", 10, 0, 0),
        };

        var standings = StandingsCalculator.Rank(totals);

        Assert.Equal(new[] { 1, 2, 2, 4 }, standings.Select(s => s.Position));
    }

    [Fact]
    public void Ties_are_listed_alphabetically()
    {
        var totals = new[]
        {
            new PlayerTotals(1, "zed", 30, 0, 0),
            new PlayerTotals(2, "Alex", 30, 0, 0),
        };

        var standings = StandingsCalculator.Rank(totals);

        Assert.Equal(new[] { "Alex", "zed" }, standings.Select(s => s.Name));
    }

    [Fact]
    public void Player_with_no_games_has_zero_win_rate_instead_of_dividing_by_zero()
    {
        var standings = StandingsCalculator.Rank(new[] { new PlayerTotals(1, "Sam", 0, 0, 0) });

        Assert.Equal(0, standings[0].GamesPlayed);
        Assert.Equal(0.0, standings[0].WinRate);
    }

    [Fact]
    public void Win_rate_is_a_rounded_percentage()
    {
        var standings = StandingsCalculator.Rank(new[] { new PlayerTotals(1, "Ben", 20, 2, 1) });

        Assert.Equal(66.7, standings[0].WinRate);
    }

    [Fact]
    public void Empty_input_returns_empty_standings()
    {
        Assert.Empty(StandingsCalculator.Rank(Array.Empty<PlayerTotals>()));
    }
}
