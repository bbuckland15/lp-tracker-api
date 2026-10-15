namespace LpTracker.Core.Matches;

/// <summary>One player's result in a submitted match.</summary>
public record PlayerResult(int PlayerId, bool Won, int LpDelta);

/// <summary>
/// Business rules for recording a match, checked before anything touches the database.
/// Returns a list of problems; an empty list means the match is valid.
/// </summary>
public static class MatchResultValidator
{
    /// <summary>Largest LP swing we accept for a single game. Guards against typos like 210 instead of 21.</summary>
    public const int MaxLpPerGame = 100;

    public static List<string> Validate(IReadOnlyCollection<PlayerResult>? results, DateTime playedAt, DateTime utcNow)
    {
        var errors = new List<string>();

        if (results is null || results.Count == 0)
        {
            errors.Add("A match needs at least one player result.");
            return errors;
        }

        if (playedAt > utcNow.AddMinutes(5))
            errors.Add("PlayedAt can't be in the future.");

        var duplicateIds = results
            .GroupBy(r => r.PlayerId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var id in duplicateIds)
            errors.Add($"Player {id} appears more than once in this match.");

        foreach (var r in results)
        {
            if (r.Won && r.LpDelta < 0)
                errors.Add($"Player {r.PlayerId} won but has negative LP ({r.LpDelta}).");

            if (!r.Won && r.LpDelta > 0)
                errors.Add($"Player {r.PlayerId} lost but has positive LP ({r.LpDelta}).");

            if (Math.Abs(r.LpDelta) > MaxLpPerGame)
                errors.Add($"Player {r.PlayerId} has an LP change of {r.LpDelta}, more than {MaxLpPerGame} in one game.");
        }

        return errors;
    }
}
