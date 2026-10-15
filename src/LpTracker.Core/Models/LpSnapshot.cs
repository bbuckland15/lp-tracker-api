namespace LpTracker.Core.Models;

/// <summary>
/// A player's ranked state as Riot reported it at one moment.
/// The Riot sync saves one per poll and compares it with the previous one
/// to work out what changed.
/// </summary>
public class LpSnapshot
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public DateTime TakenAt { get; set; }

    public string Tier { get; set; } = "";      // e.g. "GOLD"
    public string Division { get; set; } = "";  // e.g. "II" (Riot calls this "rank")
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }               // season total, solo queue
    public int Losses { get; set; }
}
