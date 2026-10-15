using LpTracker.Api.Contracts;
using LpTracker.Api.Data;
using LpTracker.Api.Riot;
using LpTracker.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LpTracker.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;

    // AppDbContext is handed to us by ASP.NET Core's dependency injection (registered in Program.cs).
    public PlayersController(AppDbContext db) => _db = db;

    /// <summary>All tracked players, alphabetical.</summary>
    [HttpGet]
    public async Task<List<PlayerResponse>> GetAll(CancellationToken ct) =>
        await _db.Players
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PlayerResponse(p.Id, p.Name, p.CreatedAt, p.RiotId))
            .ToListAsync(ct);

    /// <summary>Add a player to the leaderboard.</summary>
    [HttpPost]
    public async Task<ActionResult<PlayerResponse>> Create(CreatePlayerRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();

        // Friendly check first; the unique index on Name is the real guarantee.
        if (await _db.Players.AnyAsync(p => p.Name == name, ct))
            return Conflict(new { error = $"A player named '{name}' already exists." });

        var player = new Player { Name = name };
        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);

        var response = new PlayerResponse(player.Id, player.Name, player.CreatedAt, player.RiotId);
        return CreatedAtAction(nameof(GetHistory), new { id = player.Id }, response);
    }

    /// <summary>
    /// Link a player to their Riot account so their ranked games are imported automatically.
    /// Looks up the Riot ID with Riot's Account API and stores the player's PUUID.
    /// </summary>
    [HttpPut("{id:int}/riot-account")]
    public async Task<ActionResult<PlayerResponse>> LinkRiotAccount(
        int id,
        LinkRiotAccountRequest request,
        [FromServices] IRiotApiClient riot,
        [FromServices] IOptions<RiotOptions> riotOptions,
        CancellationToken ct)
    {
        if (!riotOptions.Value.IsConfigured)
            return Problem("No Riot API key is configured. See the README.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var player = await _db.Players.FindAsync(new object[] { id }, ct);
        if (player is null)
            return NotFound();

        // "Ben#NA1" -> ("Ben", "NA1"). Game names can contain spaces but not '#'.
        var parts = request.RiotId.Trim().Split('#');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return BadRequest(new { error = "Riot ID must look like Name#TAG." });

        var account = await riot.GetAccountByRiotIdAsync(parts[0], parts[1], ct);
        if (account is null)
            return NotFound(new { error = $"Riot has no account named '{request.RiotId}'." });

        if (await _db.Players.AnyAsync(p => p.Puuid == account.Puuid && p.Id != id, ct))
            return Conflict(new { error = "That Riot account is already linked to another player." });

        player.Puuid = account.Puuid;
        player.RiotId = $"{account.GameName}#{account.TagLine}";
        await _db.SaveChangesAsync(ct);

        return new PlayerResponse(player.Id, player.Name, player.CreatedAt, player.RiotId);
    }

    /// <summary>
    /// A player's LP game by game, with a running total. This is the history
    /// the old text-file version threw away on every save.
    /// </summary>
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<List<LpHistoryPoint>>> GetHistory(int id, CancellationToken ct)
    {
        if (!await _db.Players.AnyAsync(p => p.Id == id, ct))
            return NotFound();

        var games = await _db.LpChanges
            .AsNoTracking()
            .Where(c => c.PlayerId == id)
            .OrderBy(c => c.Match!.PlayedAt)
            .ThenBy(c => c.MatchId)
            .Select(c => new { c.MatchId, c.Match!.PlayedAt, c.Won, c.LpDelta, c.IsEstimated })
            .ToListAsync(ct);

        // Running total computed in C# to keep the query simple.
        // In SQL this would be SUM(LpDelta) OVER (ORDER BY PlayedAt) - see docs/queries.sql.
        var runningTotal = 0;
        var history = new List<LpHistoryPoint>(games.Count);
        foreach (var g in games)
        {
            runningTotal += g.LpDelta;
            history.Add(new LpHistoryPoint(g.MatchId, g.PlayedAt, g.Won, g.LpDelta, g.IsEstimated, runningTotal));
        }

        return history;
    }
}
