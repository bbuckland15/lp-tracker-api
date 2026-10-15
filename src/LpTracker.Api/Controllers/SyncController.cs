using LpTracker.Api.Riot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LpTracker.Api.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    /// <summary>
    /// Check Riot for new games right now instead of waiting for the background worker.
    /// Handy for demos and for testing your API key.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncSummary>> SyncNow(
        [FromServices] RiotSyncService sync,
        [FromServices] IOptions<RiotOptions> options,
        CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            return Problem("No Riot API key is configured. See the README.", statusCode: StatusCodes.Status503ServiceUnavailable);

        try
        {
            return await sync.SyncAllAsync(ct);
        }
        catch (RiotRateLimitException ex)
        {
            Response.Headers.RetryAfter = ((int)Math.Ceiling(ex.RetryAfter.TotalSeconds)).ToString();
            return Problem(ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
        }
    }
}
