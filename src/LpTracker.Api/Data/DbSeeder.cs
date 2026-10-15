using LpTracker.Core.Models;

namespace LpTracker.Api.Data;

/// <summary>Adds a few sample players and games so the API has data to show on first run.</summary>
public static class DbSeeder
{
    public static void SeedIfEmpty(AppDbContext db)
    {
        if (db.Players.Any())
            return;

        var ben = new Player { Name = "Ben" };
        var alex = new Player { Name = "Alex" };
        var jordan = new Player { Name = "Jordan" };
        var sam = new Player { Name = "Sam" }; // no games yet - shows up with 0 LP
        db.Players.AddRange(ben, alex, jordan, sam);

        var start = DateTime.UtcNow.Date.AddDays(-3);

        db.Matches.AddRange(
            Game(start.AddHours(19), (ben, true, 21), (alex, true, 19)),
            Game(start.AddHours(20), (ben, false, -18), (jordan, true, 22)),
            Game(start.AddDays(1).AddHours(21), (alex, true, 20), (jordan, false, -17)),
            Game(start.AddDays(2).AddHours(18), (ben, true, 24), (jordan, true, 20)));

        db.SaveChanges();
    }

    private static Match Game(DateTime playedAt, params (Player player, bool won, int lp)[] results) => new()
    {
        PlayedAt = playedAt,
        LpChanges = results
            .Select(r => new LpChange { Player = r.player, Won = r.won, LpDelta = r.lp })
            .ToList()
    };
}
