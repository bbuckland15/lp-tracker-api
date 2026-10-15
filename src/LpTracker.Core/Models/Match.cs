namespace LpTracker.Core.Models;

/// <summary>One game. Each tracked player in it gets one LpChange row.</summary>
public class Match
{
    public int Id { get; set; }

    public DateTime PlayedAt { get; set; }

    /// <summary>
    /// Riot's match ID (e.g. "NA1_4987654321") for games pulled from the Riot API;
    /// null for games entered by hand. Unique, so the same game is never imported twice,
    /// and friends who played together share one Match row.
    /// </summary>
    public string? RiotMatchId { get; set; }

    public List<LpChange> LpChanges { get; set; } = new();
}
