namespace LpTracker.Core.Ranked;

/// <summary>
/// Converts a rank like "GOLD II, 45 LP" into one number on a single ladder,
/// so the LP change between two ranks is just subtraction.
///
/// Why this exists: Riot only reports a player's *current* LP within their division.
/// Going from Gold I 90 LP to Platinum IV 10 LP looks like -80 if you subtract LP
/// directly, but it was really +20. Converting both to ladder points fixes that.
///
///   Iron IV 0 LP      =    0
///   Iron III 0 LP     =  100
///   Bronze IV 0 LP    =  400
///   ...each tier below Master is 4 divisions x 100 LP = 400 points
///   Master 0 LP       = 2800   (Master, Grandmaster and Challenger share one LP scale)
/// </summary>
public static class RankedLadder
{
    /// <summary>Tiers that have divisions IV - I, lowest first.</summary>
    public static readonly IReadOnlyList<string> DivisionTiers = new[]
    {
        "IRON", "BRONZE", "SILVER", "GOLD", "PLATINUM", "EMERALD", "DIAMOND"
    };

    /// <summary>Apex tiers: no divisions, LP keeps counting up.</summary>
    public static readonly IReadOnlyList<string> ApexTiers = new[] { "MASTER", "GRANDMASTER", "CHALLENGER" };

    private static readonly string[] Divisions = { "IV", "III", "II", "I" };

    public const int PointsPerDivision = 100;
    public const int PointsPerTier = 4 * PointsPerDivision;

    /// <summary>Ladder points where Master starts.</summary>
    public static int ApexFloor => DivisionTiers.Count * PointsPerTier;

    public static bool IsApex(string tier) => ApexTiers.Contains(tier.ToUpperInvariant());

    /// <param name="tier">Riot's tier string, e.g. "GOLD".</param>
    /// <param name="division">Riot's rank string, "IV" to "I". Ignored for apex tiers.</param>
    /// <param name="leaguePoints">LP within the division.</param>
    public static int ToLadderPoints(string tier, string division, int leaguePoints)
    {
        var t = tier.ToUpperInvariant();

        if (IsApex(t))
            return ApexFloor + leaguePoints;

        var tierIndex = IndexOf(DivisionTiers, t)
            ?? throw new ArgumentException($"Unknown tier '{tier}'.", nameof(tier));

        var divisionIndex = IndexOf(Divisions, division.ToUpperInvariant())
            ?? throw new ArgumentException($"Unknown division '{division}'.", nameof(division));

        return tierIndex * PointsPerTier + divisionIndex * PointsPerDivision + leaguePoints;
    }

    private static int? IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return null;
    }
}
