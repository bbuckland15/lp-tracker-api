namespace LpTracker.Core.Models;

/// <summary>A friend being tracked on the leaderboard.</summary>
public class Player
{
    public int Id { get; set; }

    /// <summary>Display name on the leaderboard. Unique.</summary>
    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Riot ID as typed, e.g. "Ben#NA1". Null until the account is linked.</summary>
    public string? RiotId { get; set; }

    /// <summary>Riot's permanent player ID, used for every Riot API call. Null until linked.</summary>
    public string? Puuid { get; set; }

    public List<LpChange> LpChanges { get; set; } = new();

    public List<LpSnapshot> Snapshots { get; set; } = new();
}
