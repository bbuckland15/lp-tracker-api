using LpTracker.Api.Data;
using LpTracker.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LpTracker.Tests;

/// <summary>
/// Tests that run the real standings query against a real (in-memory) SQLite database,
/// so they check the SQL that EF Core generates, not just the C# ranking logic.
/// Each test gets a fresh, empty database.
/// </summary>
public class StandingsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public StandingsQueryTests()
    {
        // An in-memory SQLite database lives as long as its connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Player AddPlayer(string name)
    {
        var player = new Player { Name = name };
        _db.Players.Add(player);
        _db.SaveChanges();
        return player;
    }

    private void AddGame(params (Player player, bool won, int lp)[] results)
    {
        _db.Matches.Add(new Match
        {
            PlayedAt = DateTime.UtcNow,
            LpChanges = results
                .Select(r => new LpChange { PlayerId = r.player.Id, Won = r.won, LpDelta = r.lp })
                .ToList()
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Wins_add_lp_and_losses_subtract_it()
    {
        var ben = AddPlayer("Ben");
        AddGame((ben, true, 21));
        AddGame((ben, false, -18));
        AddGame((ben, true, 20));

        var standings = await StandingsQuery.GetStandingsAsync(_db);

        var row = Assert.Single(standings);
        Assert.Equal(23, row.TotalLp);
        Assert.Equal(2, row.Wins);
        Assert.Equal(1, row.Losses);
    }

    [Fact]
    public async Task Player_with_no_games_still_appears_with_zero_lp()
    {
        // The bug this guards against: an INNER JOIN from Players to LpChanges
        // silently drops anyone who hasn't played yet.
        var ben = AddPlayer("Ben");
        AddPlayer("Sam");
        AddGame((ben, true, 21));

        var standings = await StandingsQuery.GetStandingsAsync(_db);

        Assert.Equal(2, standings.Count);
        var sam = Assert.Single(standings, s => s.Name == "Sam");
        Assert.Equal(0, sam.TotalLp);
        Assert.Equal(2, sam.Position);
    }

    [Fact]
    public async Task Players_are_ranked_across_multiple_matches()
    {
        var ben = AddPlayer("Ben");
        var alex = AddPlayer("Alex");
        var jordan = AddPlayer("Jordan");

        AddGame((ben, true, 20), (alex, true, 20));
        AddGame((jordan, true, 25));
        AddGame((alex, false, -15));

        var standings = await StandingsQuery.GetStandingsAsync(_db);

        Assert.Equal(new[] { "Jordan", "Ben", "Alex" }, standings.Select(s => s.Name));
    }

    [Fact]
    public async Task Deleting_a_match_removes_its_lp_from_the_standings()
    {
        var ben = AddPlayer("Ben");
        AddGame((ben, true, 20));
        AddGame((ben, true, 20)); // entered by mistake

        var mistake = _db.Matches.OrderByDescending(m => m.Id).First();
        _db.Matches.Remove(mistake);
        _db.SaveChanges();

        var standings = await StandingsQuery.GetStandingsAsync(_db);

        Assert.Equal(20, Assert.Single(standings).TotalLp);
        Assert.Equal(1, _db.LpChanges.Count()); // cascade delete removed the row
    }

    [Fact]
    public void Same_player_twice_in_one_match_is_blocked_by_the_database()
    {
        // The validator catches this first, but the unique index is the real safety net.
        var ben = AddPlayer("Ben");

        Assert.Throws<DbUpdateException>(() => AddGame((ben, true, 20), (ben, true, 20)));
    }

    [Fact]
    public void Duplicate_player_names_are_blocked_by_the_database()
    {
        AddPlayer("Ben");

        Assert.Throws<DbUpdateException>(() => AddPlayer("Ben"));
    }
}
