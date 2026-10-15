namespace LpTracker.Core.Ranked;

/// <summary>A player's ranked solo queue state at one moment, as Riot reports it.</summary>
public record RankedState(string Tier, string Division, int LeaguePoints, int Wins, int Losses)
{
    public int GamesPlayed => Wins + Losses;
    public int LadderPoints => RankedLadder.ToLadderPoints(Tier, Division, LeaguePoints);
}

/// <summary>What changed between two snapshots of the same player.</summary>
public record RankedDiff(int GamesPlayed, int Wins, int Losses, int LpChange);

/// <summary>The LP to record for one game.</summary>
public record GameLp(bool Won, int LpDelta, bool IsEstimated);

/// <summary>
/// Works out per-game LP from two snapshots of a player's rank.
///
/// Riot's match data says who won each game but not how much LP changed.
/// Riot's ranked data has the current LP but not per game. So we take a snapshot
/// every poll and compare it with the last one.
/// </summary>
public static class LpDeltaCalculator
{
    /// <summary>
    /// Compares two snapshots. Returns null when there are no new games to record:
    /// nothing was played, or the win/loss count went down (a new season reset the record).
    /// </summary>
    public static RankedDiff? Compare(RankedState previous, RankedState current)
    {
        var games = current.GamesPlayed - previous.GamesPlayed;
        if (games <= 0)
            return null;

        return new RankedDiff(
            GamesPlayed: games,
            Wins: current.Wins - previous.Wins,
            Losses: current.Losses - previous.Losses,
            LpChange: current.LadderPoints - previous.LadderPoints);
    }

    /// <summary>
    /// Splits the total LP change across the new games.
    ///
    /// One game since the last snapshot (the normal case when polling every few minutes):
    /// the LP is exact.
    ///
    /// Several games (e.g. the tracker was offline): we only know the total, so it is
    /// split evenly and each row is marked IsEstimated. The total, and therefore the
    /// standings, stay exact; only the per-game breakdown is approximate.
    /// </summary>
    /// <param name="totalLp">Net LP change across all the games.</param>
    /// <param name="gameResults">Win (true) or loss (false) for each game, oldest first.</param>
    public static List<GameLp> SplitAcrossGames(int totalLp, IReadOnlyList<bool> gameResults)
    {
        if (gameResults.Count == 0)
            return new List<GameLp>();

        if (gameResults.Count == 1)
            return new List<GameLp> { new(gameResults[0], totalLp, IsEstimated: false) };

        // Even split. Integer division can leave a remainder, which is handed out one LP
        // at a time to the first games so the parts always add back up to the total.
        int count = gameResults.Count;
        int baseShare = totalLp / count;
        int remainder = totalLp - baseShare * count;   // same sign as totalLp
        int step = Math.Sign(remainder);

        var result = new List<GameLp>(count);
        for (int i = 0; i < count; i++)
        {
            int extra = i < Math.Abs(remainder) ? step : 0;
            result.Add(new GameLp(gameResults[i], baseShare + extra, IsEstimated: true));
        }

        return result;
    }
}
