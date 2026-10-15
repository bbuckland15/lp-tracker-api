using LpTracker.Core.Standings;
using Microsoft.EntityFrameworkCore;

namespace LpTracker.Api.Data;

/// <summary>
/// Reads standings from the database.
///
/// The database sums each player's LP and counts wins/losses (one grouped query);
/// StandingsCalculator then assigns positions. Starting from Players (not LpChanges)
/// means a player with zero games still appears, with 0 LP. In SQL terms that's a
/// LEFT JOIN with COALESCE(SUM(...), 0) - see docs/queries.sql.
/// </summary>
public static class StandingsQuery
{
    public static async Task<List<StandingRow>> GetStandingsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var totals = await db.Players
            .AsNoTracking()
            .Select(p => new PlayerTotals(
                p.Id,
                p.Name,
                p.LpChanges.Sum(c => c.LpDelta),
                p.LpChanges.Count(c => c.Won),
                p.LpChanges.Count(c => !c.Won)))
            .ToListAsync(ct);

        return StandingsCalculator.Rank(totals);
    }
}
