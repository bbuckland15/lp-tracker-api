using System.Net;
using System.Net.Http.Json;
using LpTracker.Core.Ranked;
using Microsoft.Extensions.Options;

namespace LpTracker.Api.Riot;

/// <summary>
/// Calls Riot's REST API over HTTPS.
///
/// Riot uses two kinds of host:
///   platform (e.g. na1.api.riotgames.com)      -> League-V4 (ranked standings)
///   regional (e.g. americas.api.riotgames.com) -> Account-V1, Match-V5
///
/// Endpoints used (check developer.riotgames.com if Riot changes them):
///   GET /riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}
///   GET /lol/league/v4/entries/by-puuid/{puuid}
///   GET /lol/match/v5/matches/by-puuid/{puuid}/ids?queue=420&amp;startTime=..&amp;count=..
///   GET /lol/match/v5/matches/{matchId}
/// </summary>
public class RiotApiClient : IRiotApiClient
{
    /// <summary>Riot's queue ID for Ranked Solo/Duo.</summary>
    public const int RankedSoloQueueId = 420;
    private const string RankedSoloQueueType = "RANKED_SOLO_5x5";

    private readonly HttpClient _http;
    private readonly RiotOptions _options;

    // HttpClient is created and managed by IHttpClientFactory (see Program.cs),
    // which avoids the classic "new HttpClient() per request" socket exhaustion problem.
    public RiotApiClient(HttpClient http, IOptions<RiotOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    private string PlatformHost => $"https://{_options.Platform}.api.riotgames.com";
    private string RegionHost => $"https://{_options.Region}.api.riotgames.com";

    public async Task<RiotAccount?> GetAccountByRiotIdAsync(string gameName, string tagLine, CancellationToken ct = default)
    {
        var url = $"{RegionHost}/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";
        var dto = await GetAsync<AccountDto>(url, ct);
        return dto is null ? null : new RiotAccount(dto.Puuid, dto.GameName ?? gameName, dto.TagLine ?? tagLine);
    }

    public async Task<RankedState?> GetSoloQueueRankAsync(string puuid, CancellationToken ct = default)
    {
        var url = $"{PlatformHost}/lol/league/v4/entries/by-puuid/{Uri.EscapeDataString(puuid)}";
        var entries = await GetAsync<List<LeagueEntryDto>>(url, ct);

        // A player has one entry per ranked queue they've played this season; we only track solo/duo.
        var solo = entries?.FirstOrDefault(e => e.QueueType == RankedSoloQueueType);
        return solo is null ? null : new RankedState(solo.Tier, solo.Rank, solo.LeaguePoints, solo.Wins, solo.Losses);
    }

    public async Task<IReadOnlyList<string>> GetRankedMatchIdsAsync(string puuid, DateTime since, int count, CancellationToken ct = default)
    {
        var startTime = new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var safeCount = Math.Clamp(count, 1, 100); // Riot allows 0-100

        var url = $"{RegionHost}/lol/match/v5/matches/by-puuid/{Uri.EscapeDataString(puuid)}/ids" +
                  $"?queue={RankedSoloQueueId}&startTime={startTime}&count={safeCount}";

        return await GetAsync<List<string>>(url, ct) ?? new List<string>();
    }

    public async Task<RiotMatchInfo?> GetMatchAsync(string matchId, CancellationToken ct = default)
    {
        var url = $"{RegionHost}/lol/match/v5/matches/{Uri.EscapeDataString(matchId)}";
        var dto = await GetAsync<MatchDto>(url, ct);
        if (dto is null)
            return null;

        // gameEndTimestamp is in milliseconds. Very old matches don't have it, so fall back to the start time.
        var endedMs = dto.Info.GameEndTimestamp > 0 ? dto.Info.GameEndTimestamp : dto.Info.GameCreation;
        var endedAt = DateTimeOffset.FromUnixTimeMilliseconds(endedMs).UtcDateTime;

        var winByPuuid = dto.Info.Participants
            .Where(p => !string.IsNullOrEmpty(p.Puuid))
            .GroupBy(p => p.Puuid)
            .ToDictionary(g => g.Key, g => g.First().Win);

        return new RiotMatchInfo(dto.Metadata.MatchId, endedAt, winByPuuid);
    }

    /// <summary>GET a URL and read JSON. 404 -> null, 429 -> RiotRateLimitException, other errors throw.</summary>
    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
            throw new RiotRateLimitException(retryAfter);
        }

        // 401/403 usually means a missing or expired key (development keys expire every 24 hours).
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    // ---- Riot's JSON shapes (only the fields we use). Property names match Riot's camelCase JSON. ----

    private record AccountDto(string Puuid, string? GameName, string? TagLine);

    private record LeagueEntryDto(string QueueType, string Tier, string Rank, int LeaguePoints, int Wins, int Losses);

    private record MatchDto(MatchMetadataDto Metadata, MatchInfoDto Info);

    private record MatchMetadataDto(string MatchId);

    private record MatchInfoDto(long GameCreation, long GameEndTimestamp, int QueueId, List<ParticipantDto> Participants);

    private record ParticipantDto(string Puuid, bool Win);
}
