namespace LpTracker.Core.Standings;

/// <summary>Per-player totals, as summed up by the database.</summary>
public record PlayerTotals(int PlayerId, string Name, int TotalLp, int Wins, int Losses);

/// <summary>One row of the leaderboard.</summary>
public record StandingRow(int Position, int PlayerId, string Name, int TotalLp, int Wins, int Losses)
{
    public int GamesPlayed => Wins + Losses;

    /// <summary>Win rate as a percentage, 0 when no games have been played.</summary>
    public double WinRate => GamesPlayed == 0 ? 0 : Math.Round(100.0 * Wins / GamesPlayed, 1);
}

/// <summary>
/// Turns per-player totals into ranked standings.
///
/// Kept as plain C# with no database dependency so it can be unit tested directly.
/// The database does the heavy lifting (SUM / COUNT grouped by player); this class
/// only decides the order and positions.
/// </summary>
public static class StandingsCalculator
{
    /// <summary>
    /// Ranks players by total LP, highest first.
    /// Ties share a position and the next position is skipped ("1, 1, 3"),
    /// the same result as SQL's RANK() window function.
    /// </summary>
    public static List<StandingRow> Rank(IEnumerable<PlayerTotals> totals)
    {
        var ordered = totals
            .OrderByDescending(t => t.TotalLp)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase) // stable, readable order within a tie
            .ToList();

        var rows = new List<StandingRow>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];

            // Same LP as the player above -> same position. Otherwise position = place in list.
            int position = (i > 0 && t.TotalLp == ordered[i - 1].TotalLp)
                ? rows[i - 1].Position
                : i + 1;

            rows.Add(new StandingRow(position, t.PlayerId, t.Name, t.TotalLp, t.Wins, t.Losses));
        }

        return rows;
    }
}
