using LpTracker.Api.Data;
using LpTracker.Core.Models;
using LpTracker.Core.Ranked;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LpTracker.Api.Riot;

public record SyncSummary(int PlayersChecked, int GamesImported, List<string> Notes);

/// <summary>
/// Pulls new ranked games from Riot and records them.
///
/// For each linked player:
///   1. Get their current rank from Riot (tier, division, LP, season wins/losses).
///   2. Compare it with the last snapshot we saved.
///   3. If they played new games, fetch those matches to find out when they ended and
///      whether each was a win, then split the LP change across them.
///   4. Save the new Match/LpChange rows and the new snapshot in one transaction.
/// </summary>
public class RiotSyncService
{
    // Only one sync at a time (the background worker and POST /api/sync could overlap).
    // The unique indexes on RiotMatchId and (MatchId, PlayerId) are the backstop if they ever did.
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private readonly AppDbContext _db;
    private readonly IRiotApiClient _riot;
    private readonly RiotOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<RiotSyncService> _logger;

    public RiotSyncService(
        AppDbContext db,
        IRiotApiClient riot,
        IOptions<RiotOptions> options,
        TimeProvider clock,
        ILogger<RiotSyncService> logger)
    {
        _db = db;
        _riot = riot;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SyncSummary> SyncAllAsync(CancellationToken ct = default)
    {
        await SyncLock.WaitAsync(ct);
        try
        {
            var players = await _db.Players
                .Where(p => p.Puuid != null)
                .OrderBy(p => p.Id)
                .ToListAsync(ct);

            var notes = new List<string>();
            var imported = 0;

            foreach (var player in players)
            {
                try
                {
                    imported += await SyncPlayerAsync(player, notes, ct);
                }
                catch (RiotRateLimitException)
                {
                    // Stop the whole run; the worker waits and tries again later.
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or DbUpdateException)
                {
                    // One player's failure shouldn't stop everyone else's update.
                    _logger.LogWarning(ex, "Riot sync failed for {Player}", player.Name);
                    notes.Add($"{player.Name}: sync failed ({ex.Message}).");
                    _db.ChangeTracker.Clear(); // drop that player's half-built changes
                }
            }

            return new SyncSummary(players.Count, imported, notes);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <returns>Number of games imported for this player.</returns>
    private async Task<int> SyncPlayerAsync(Player player, List<string> notes, CancellationToken ct)
    {
        var puuid = player.Puuid!;
        var now = _clock.GetUtcNow().UtcDateTime;

        var current = await _riot.GetSoloQueueRankAsync(puuid, ct);
        if (current is null)
        {
            notes.Add($"{player.Name}: unranked in solo/duo, skipped.");
            return 0;
        }

        var last = await _db.Set<LpSnapshot>()
            .Where(s => s.PlayerId == player.Id)
            .OrderByDescending(s => s.TakenAt)
            .FirstOrDefaultAsync(ct);

        // First time we've seen this player: record a baseline. Games before this
        // point aren't imported because we don't know their LP.
        if (last is null)
        {
            AddSnapshot(player.Id, current, now);
            await _db.SaveChangesAsync(ct);
            notes.Add($"{player.Name}: baseline saved ({current.Tier} {current.Division} {current.LeaguePoints} LP).");
            return 0;
        }

        var previous = new RankedState(last.Tier, last.Division, last.LeaguePoints, last.Wins, last.Losses);
        var diff = LpDeltaCalculator.Compare(previous, current);

        if (diff is null)
        {
            // No new games. If something else changed (dodge penalty, new season), move the baseline.
            if (current != previous)
            {
                AddSnapshot(player.Id, current, now);
                await _db.SaveChangesAsync(ct);
            }
            return 0;
        }

        // Find the new games in match history.
        var since = last.TakenAt.AddMinutes(-_options.MatchLookbackMinutes);
        var matchIds = await _riot.GetRankedMatchIdsAsync(puuid, since, diff.GamesPlayed + 10, ct);

        var alreadyRecorded = await _db.LpChanges
            .Where(c => c.PlayerId == player.Id && c.Match!.RiotMatchId != null)
            .Select(c => c.Match!.RiotMatchId!)
            .ToListAsync(ct);

        var newGames = new List<RiotMatchInfo>();
        foreach (var id in matchIds.Where(id => !alreadyRecorded.Contains(id)).Take(diff.GamesPlayed))
        {
            var match = await _riot.GetMatchAsync(id, ct);
            if (match is not null && match.WinByPuuid.ContainsKey(puuid))
                newGames.Add(match);
        }

        // Riot's match history can lag a few minutes behind the ranked numbers.
        // Don't save a snapshot yet, so the next poll tries again with the same baseline.
        if (newGames.Count < diff.GamesPlayed)
        {
            notes.Add($"{player.Name}: {diff.GamesPlayed} new game(s) but only {newGames.Count} in match history yet; will retry.");
            return 0;
        }

        newGames = newGames.OrderBy(g => g.EndedAt).ToList();
        var lpPerGame = LpDeltaCalculator.SplitAcrossGames(
            diff.LpChange,
            newGames.Select(g => g.WinByPuuid[puuid]).ToList());

        for (int i = 0; i < newGames.Count; i++)
        {
            var match = await FindOrCreateMatchAsync(newGames[i], ct);
            match.LpChanges.Add(new LpChange
            {
                PlayerId = player.Id,
                Won = lpPerGame[i].Won,
                LpDelta = lpPerGame[i].LpDelta,
                IsEstimated = lpPerGame[i].IsEstimated
            });
        }

        AddSnapshot(player.Id, current, now);

        // Matches, LP changes and the snapshot all save together, or not at all.
        await _db.SaveChangesAsync(ct);

        var estimated = newGames.Count > 1 ? " (split evenly, estimated)" : "";
        notes.Add($"{player.Name}: imported {newGames.Count} game(s), {diff.LpChange:+#;-#;0} LP{estimated}.");
        return newGames.Count;
    }

    /// <summary>
    /// Friends who played in the same game share one Match row, so look for it first:
    /// in what this run already added, then in the database.
    /// </summary>
    private async Task<Match> FindOrCreateMatchAsync(RiotMatchInfo info, CancellationToken ct)
    {
        var match = _db.Matches.Local.FirstOrDefault(m => m.RiotMatchId == info.MatchId)
                    ?? await _db.Matches
                        .Include(m => m.LpChanges)
                        .FirstOrDefaultAsync(m => m.RiotMatchId == info.MatchId, ct);

        if (match is null)
        {
            match = new Match { RiotMatchId = info.MatchId, PlayedAt = info.EndedAt };
            _db.Matches.Add(match);
        }

        return match;
    }

    private void AddSnapshot(int playerId, RankedState state, DateTime takenAt) =>
        _db.Set<LpSnapshot>().Add(new LpSnapshot
        {
            PlayerId = playerId,
            TakenAt = takenAt,
            Tier = state.Tier,
            Division = state.Division,
            LeaguePoints = state.LeaguePoints,
            Wins = state.Wins,
            Losses = state.Losses
        });
}
