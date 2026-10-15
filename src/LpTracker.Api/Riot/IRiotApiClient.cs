using LpTracker.Core.Ranked;

namespace LpTracker.Api.Riot;

public record RiotAccount(string Puuid, string GameName, string TagLine);

/// <summary>The parts of a Riot match we care about.</summary>
/// <param name="WinByPuuid">Every participant's PUUID mapped to whether they won.</param>
public record RiotMatchInfo(string MatchId, DateTime EndedAt, IReadOnlyDictionary<string, bool> WinByPuuid);

/// <summary>
/// The Riot API calls this app needs.
/// It's an interface so the sync logic can be tested with a fake client,
/// without a real API key or network access.
/// </summary>
public interface IRiotApiClient
{
    /// <summary>Looks up a Riot ID ("Name#TAG"). Null if no such account.</summary>
    Task<RiotAccount?> GetAccountByRiotIdAsync(string gameName, string tagLine, CancellationToken ct = default);

    /// <summary>Current ranked solo/duo standing. Null if the player is unranked this season.</summary>
    Task<RankedState?> GetSoloQueueRankAsync(string puuid, CancellationToken ct = default);

    /// <summary>Ranked solo/duo match IDs that started at or after <paramref name="since"/>, newest first.</summary>
    Task<IReadOnlyList<string>> GetRankedMatchIdsAsync(string puuid, DateTime since, int count, CancellationToken ct = default);

    /// <summary>One match's result. Null if Riot doesn't have it.</summary>
    Task<RiotMatchInfo?> GetMatchAsync(string matchId, CancellationToken ct = default);
}

/// <summary>Riot returned 429 Too Many Requests.</summary>
public class RiotRateLimitException : Exception
{
    public TimeSpan RetryAfter { get; }

    public RiotRateLimitException(TimeSpan retryAfter)
        : base($"Riot API rate limit hit. Retry after {retryAfter.TotalSeconds:0}s.")
        => RetryAfter = retryAfter;
}
