using LpTracker.Api.Contracts;
using LpTracker.Api.Data;
using LpTracker.Core.Matches;
using LpTracker.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LpTracker.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MatchesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Record one game's results. Inserts one Match row and one LpChange row per player.
    /// Nothing that already exists is rewritten.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MatchResponse>> Record(RecordMatchRequest request, CancellationToken ct)
    {
        var playedAt = request.PlayedAt ?? DateTime.UtcNow;

        // 1. Business rules (no database needed).
        var errors = MatchResultValidator.Validate(request.Results, playedAt, DateTime.UtcNow);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        // 2. Every player must exist.
        var requestedIds = request.Results.Select(r => r.PlayerId).Distinct().ToList();
        var players = await _db.Players
            .Where(p => requestedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var missing = requestedIds.Where(id => !players.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            return BadRequest(new { errors = missing.Select(id => $"Player {id} does not exist.").ToList() });

        // 3. Save. The match and all of its LP changes go in one SaveChanges call,
        //    which EF Core wraps in a single transaction: either everything saves or nothing does.
        //    (The old version could leave a half-written text file if it crashed mid-save.)
        var match = new Match
        {
            PlayedAt = playedAt,
            LpChanges = request.Results
                .Select(r => new LpChange { PlayerId = r.PlayerId, Won = r.Won, LpDelta = r.LpDelta })
                .ToList()
        };

        _db.Matches.Add(match);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = match.Id }, ToResponse(match, players));
    }

    /// <summary>One match and its results.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MatchResponse>> GetById(int id, CancellationToken ct)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.LpChanges)
                .ThenInclude(c => c.Player)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (match is null)
            return NotFound();

        var names = match.LpChanges.ToDictionary(c => c.PlayerId, c => c.Player!.Name);
        return ToResponse(match, names);
    }

    /// <summary>
    /// Remove a match entered by mistake. Its LP changes are deleted with it (cascade),
    /// and standings correct themselves because they're always calculated from the rows.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var match = await _db.Matches.FindAsync(new object[] { id }, ct);
        if (match is null)
            return NotFound();

        _db.Matches.Remove(match);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MatchResponse ToResponse(Match match, IReadOnlyDictionary<int, string> playerNames) =>
        new(match.Id,
            match.PlayedAt,
            match.LpChanges
                .Select(c => new MatchResultResponse(c.PlayerId, playerNames[c.PlayerId], c.Won, c.LpDelta))
                .ToList());
}
