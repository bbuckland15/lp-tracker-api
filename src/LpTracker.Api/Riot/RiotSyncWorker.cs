using Microsoft.Extensions.Options;

namespace LpTracker.Api.Riot;

/// <summary>
/// Runs in the background for as long as the API is running and syncs
/// with Riot every few minutes. Does nothing if no API key is configured.
/// </summary>
public class RiotSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RiotOptions _options;
    private readonly ILogger<RiotSyncWorker> _logger;

    public RiotSyncWorker(IServiceScopeFactory scopes, IOptions<RiotOptions> options, ILogger<RiotSyncWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("No Riot API key configured; automatic sync is off. Matches can still be entered by hand.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.PollIntervalMinutes));
        _logger.LogInformation("Riot sync running every {Minutes} minute(s).", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = interval;
            try
            {
                // The worker lives for the whole app, but AppDbContext is per-request (scoped),
                // so each run gets its own scope and its own DbContext.
                using var scope = _scopes.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<RiotSyncService>();

                var summary = await sync.SyncAllAsync(stoppingToken);
                if (summary.GamesImported > 0 || summary.Notes.Count > 0)
                    _logger.LogInformation("Riot sync: {Games} game(s) imported. {Notes}",
                        summary.GamesImported, string.Join(" ", summary.Notes));
            }
            catch (RiotRateLimitException ex)
            {
                _logger.LogWarning("{Message}", ex.Message);
                wait = ex.RetryAfter > interval ? ex.RetryAfter : interval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad run kill the worker.
                _logger.LogError(ex, "Riot sync run failed.");
            }

            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
