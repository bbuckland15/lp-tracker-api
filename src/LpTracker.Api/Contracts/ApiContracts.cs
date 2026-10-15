using System.ComponentModel.DataAnnotations;
using LpTracker.Core.Matches;

namespace LpTracker.Api.Contracts;

// Request and response shapes for the API. Keeping these separate from the
// database models means the API contract doesn't change every time the schema does.

public record CreatePlayerRequest([Required, StringLength(50, MinimumLength = 1)] string Name);

public record PlayerResponse(int Id, string Name, DateTime CreatedAt, string? RiotId);

/// <param name="RiotId">The player's Riot ID, e.g. "Ben#NA1".</param>
public record LinkRiotAccountRequest([Required] string RiotId);

/// <param name="PlayedAt">When the game was played. Defaults to now if left out.</param>
/// <param name="Results">One entry per tracked player in the game.</param>
public record RecordMatchRequest(DateTime? PlayedAt, [Required] List<PlayerResult> Results);

public record MatchResultResponse(int PlayerId, string PlayerName, bool Won, int LpDelta);

public record MatchResponse(int Id, DateTime PlayedAt, List<MatchResultResponse> Results);

public record LpHistoryPoint(int MatchId, DateTime PlayedAt, bool Won, int LpDelta, bool IsEstimated, int RunningTotal);
