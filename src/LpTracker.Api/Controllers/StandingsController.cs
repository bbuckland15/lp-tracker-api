using LpTracker.Api.Data;
using LpTracker.Core.Standings;
using Microsoft.AspNetCore.Mvc;

namespace LpTracker.Api.Controllers;

[ApiController]
[Route("api/standings")]
public class StandingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StandingsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Current leaderboard, highest LP first. Calculated from the LpChanges rows on
    /// every request instead of being stored, so it can never drift out of sync.
    /// </summary>
    [HttpGet]
    public Task<List<StandingRow>> Get(CancellationToken ct) => StandingsQuery.GetStandingsAsync(_db, ct);
}
