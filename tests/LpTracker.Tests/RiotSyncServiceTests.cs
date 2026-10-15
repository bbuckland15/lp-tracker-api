using LpTracker.Api.Data;
using LpTracker.Api.Riot;
using LpTracker.Core.Models;
using LpTracker.Core.Ranked;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LpTracker.Tests;

/// <summary>
/// Tests the Riot sync end to end against an in-memory SQLite database,
/// with a fake Riot client standing in for the real API (no key or network needed).
/// </summary>
public class RiotSyncServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeRiotClient _riot = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc));
    private readonly RiotSyncService _sync;

    public RiotSyncServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _sync = new RiotSyncService(
            _db, _riot, Options.Create(new RiotOptions { ApiKey = "test" }), _clock,
            NullLogger<RiotSyncService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Player AddLinkedPlayer(string name, string puuid)
    {
        var player = new Player { Name = name, Puuid = puuid, RiotId = $"{name}#NA1" };
        _db.Players.Add(player);
        _db.SaveChanges();
        return player;
    }

    [Fact]
    public async Task First_sync_saves_a_baseline_and_imports_nothing()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 45, 10, 8);

        var summary = await _sync.SyncAllAsync();

        Assert.Equal(0, summary.GamesImported);
        Assert.Equal(1, _db.LpSnapshots.Count());
        Assert.Equal(0, _db.LpChanges.Count());
    }

    [Fact]
    public async Task One_new_win_is_imported_with_exact_lp()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 45, 10, 8);
        await _sync.SyncAllAsync();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 66, 11, 8);
        _riot.AddMatch("NA1_1", _clock.Now.AddMinutes(-5), ("ben-puuid", true));

        var summary = await _sync.SyncAllAsync();

        Assert.Equal(1, summary.GamesImported);
        var change = Assert.Single(_db.LpChanges.Include(c => c.Match).ToList());
        Assert.True(change.Won);
        Assert.Equal(21, change.LpDelta);
        Assert.False(change.IsEstimated);
        Assert.Equal("NA1_1", change.Match!.RiotMatchId);
    }

    [Fact]
    public async Task Promotion_is_recorded_as_a_gain()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "I", 90, 20, 15);
        await _sync.SyncAllAsync();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _riot.Ranks["ben-puuid"] = new RankedState("PLATINUM", "IV", 10, 21, 15);
        _riot.AddMatch("NA1_2", _clock.Now.AddMinutes(-5), ("ben-puuid", true));

        await _sync.SyncAllAsync();

        Assert.Equal(20, Assert.Single(_db.LpChanges.ToList()).LpDelta);
    }

    [Fact]
    public async Task Running_sync_twice_does_not_import_the_same_game_twice()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 45, 10, 8);
        await _sync.SyncAllAsync();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 66, 11, 8);
        _riot.AddMatch("NA1_1", _clock.Now.AddMinutes(-5), ("ben-puuid", true));

        await _sync.SyncAllAsync();
        _clock.Advance(TimeSpan.FromMinutes(3));
        await _sync.SyncAllAsync();

        Assert.Equal(1, _db.LpChanges.Count());
        Assert.Equal(1, _db.Matches.Count());
    }

    [Fact]
    public async Task Friends_in_the_same_game_share_one_match()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        AddLinkedPlayer("Alex", "alex-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 45, 10, 8);
        _riot.Ranks["alex-puuid"] = new RankedState("SILVER", "I", 70, 5, 5);
        await _sync.SyncAllAsync();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 64, 11, 8);
        _riot.Ranks["alex-puuid"] = new RankedState("SILVER", "I", 92, 6, 5);
        _riot.AddMatch("NA1_DUO", _clock.Now.AddMinutes(-5), ("ben-puuid", true), ("alex-puuid", true));

        await _sync.SyncAllAsync();

        var match = Assert.Single(_db.Matches.Include(m => m.LpChanges).ToList());
        Assert.Equal(2, match.LpChanges.Count);
    }

    [Fact]
    public async Task Several_games_since_last_sync_are_split_and_marked_estimated()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 45, 10, 8);
        await _sync.SyncAllAsync();

        _clock.Advance(TimeSpan.FromHours(2));
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 86, 12, 8); // +41 over two wins
        _riot.AddMatch("NA1_A", _clock.Now.AddMinutes(-80), ("ben-puuid", true));
        _riot.AddMatch("NA1_B", _clock.Now.AddMinutes(-30), ("ben-puuid", true));

        await _sync.SyncAllAsync();

        var changes = _db.LpChanges.ToList();
        Assert.Equal(2, changes.Count);
        Assert.Equal(41, changes.Sum(c => c.LpDelta)); // total stays exact
        Assert.All(changes, c => Assert.True(c.IsEstimated));
    }

    [Fact]
    public async Task Waits_when_match_history_has_not_caught_up_yet()
    {
        AddLinkedPlayer("Ben", "ben-puuid");
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 45, 10, 8);
        await _sync.SyncAllAsync();

        // Rank shows a new game, but the match isn't in Riot's history yet.
        _clock.Advance(TimeSpan.FromMinutes(30));
        _riot.Ranks["ben-puuid"] = new RankedState("GOLD", "II", 66, 11, 8);

        await _sync.SyncAllAsync();
        Assert.Equal(0, _db.LpChanges.Count());
        Assert.Equal(1, _db.LpSnapshots.Count()); // baseline kept so the next run can retry

        // Next poll: the match has shown up.
        _clock.Advance(TimeSpan.FromMinutes(3));
        _riot.AddMatch("NA1_1", _clock.Now.AddMinutes(-10), ("ben-puuid", true));

        await _sync.SyncAllAsync();
        Assert.Equal(21, Assert.Single(_db.LpChanges.ToList()).LpDelta);
    }

    [Fact]
    public async Task Unlinked_and_unranked_players_are_skipped()
    {
        _db.Players.Add(new Player { Name = "NotLinked" });
        _db.SaveChanges();
        AddLinkedPlayer("Unranked", "unranked-puuid"); // no entry in _riot.Ranks

        var summary = await _sync.SyncAllAsync();

        Assert.Equal(1, summary.PlayersChecked);
        Assert.Equal(0, _db.LpSnapshots.Count());
    }

    // ---- Test doubles ------------------------------------------------------

    private class FakeRiotClient : IRiotApiClient
    {
        public Dictionary<string, RankedState> Ranks { get; } = new();
        private readonly List<RiotMatchInfo> _matches = new();

        public void AddMatch(string id, DateTime endedAt, params (string puuid, bool won)[] players) =>
            _matches.Add(new RiotMatchInfo(id, endedAt, players.ToDictionary(p => p.puuid, p => p.won)));

        public Task<RiotAccount?> GetAccountByRiotIdAsync(string gameName, string tagLine, CancellationToken ct = default) =>
            Task.FromResult<RiotAccount?>(new RiotAccount($"{gameName}-puuid", gameName, tagLine));

        public Task<RankedState?> GetSoloQueueRankAsync(string puuid, CancellationToken ct = default) =>
            Task.FromResult(Ranks.TryGetValue(puuid, out var r) ? r : null);

        public Task<IReadOnlyList<string>> GetRankedMatchIdsAsync(string puuid, DateTime since, int count, CancellationToken ct = default)
        {
            // Like Riot: this player's matches, newest first. (Uses end time; Riot filters by start time.)
            IReadOnlyList<string> ids = _matches
                .Where(m => m.WinByPuuid.ContainsKey(puuid) && m.EndedAt >= since)
                .OrderByDescending(m => m.EndedAt)
                .Take(count)
                .Select(m => m.MatchId)
                .ToList();
            return Task.FromResult(ids);
        }

        public Task<RiotMatchInfo?> GetMatchAsync(string matchId, CancellationToken ct = default) =>
            Task.FromResult(_matches.FirstOrDefault(m => m.MatchId == matchId));
    }

    private class FakeClock : TimeProvider
    {
        public DateTime Now { get; private set; }
        public FakeClock(DateTime start) => Now = start;
        public void Advance(TimeSpan by) => Now += by;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }
}
