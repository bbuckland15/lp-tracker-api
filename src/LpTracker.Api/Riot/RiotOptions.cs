namespace LpTracker.Api.Riot;

/// <summary>
/// Settings from the "Riot" section of configuration.
///
/// The API key must NOT go in appsettings.json (that file is committed to Git).
/// Store it with .NET user-secrets instead:
///     dotnet user-secrets set "Riot:ApiKey" "RGAPI-..." --project src/LpTracker.Api
/// or set the environment variable Riot__ApiKey.
/// </summary>
public class RiotOptions
{
    public const string SectionName = "Riot";

    public string? ApiKey { get; set; }

    /// <summary>Platform routing for League endpoints, e.g. "na1", "euw1".</summary>
    public string Platform { get; set; } = "na1";

    /// <summary>Regional routing for Account and Match endpoints, e.g. "americas", "europe".</summary>
    public string Region { get; set; } = "americas";

    /// <summary>How often the background worker checks for new games.</summary>
    public int PollIntervalMinutes { get; set; } = 3;

    /// <summary>
    /// How far before the last snapshot to look for matches. Riot filters match history by
    /// game START time, so a game that started before the last snapshot but ended after it
    /// would otherwise be missed.
    /// </summary>
    public int MatchLookbackMinutes { get; set; } = 120;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
