namespace LpTracker.Core.Models;

/// <summary>
/// How much LP one player gained or lost in one match.
///
/// This is the key fix over the old text-file version: every result is its own
/// row that is inserted and never rewritten, so we keep full history and can
/// correct a bad entry without touching anyone else's data.
/// </summary>
public class LpChange
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int MatchId { get; set; }
    public Match? Match { get; set; }

    public bool Won { get; set; }

    /// <summary>Positive for a win, negative for a loss (e.g. +21 / -18).</summary>
    public int LpDelta { get; set; }

    /// <summary>
    /// True when several games were imported at once and the total LP was split
    /// evenly between them. The total is still exact; this game's share is an estimate.
    /// </summary>
    public bool IsEstimated { get; set; }
}
