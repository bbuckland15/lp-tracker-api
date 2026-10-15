using LpTracker.Core.Matches;

namespace LpTracker.Tests;

public class MatchResultValidatorTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Valid_match_has_no_errors()
    {
        var results = new[] { new PlayerResult(1, true, 21), new PlayerResult(2, false, -18) };

        Assert.Empty(MatchResultValidator.Validate(results, Now, Now));
    }

    [Fact]
    public void Match_with_no_results_is_rejected()
    {
        Assert.NotEmpty(MatchResultValidator.Validate(Array.Empty<PlayerResult>(), Now, Now));
        Assert.NotEmpty(MatchResultValidator.Validate(null, Now, Now));
    }

    [Theory]
    [InlineData(true, -20)]  // won but lost LP
    [InlineData(false, 15)]  // lost but gained LP
    public void Win_or_loss_must_match_the_sign_of_the_lp_change(bool won, int lpDelta)
    {
        var errors = MatchResultValidator.Validate(new[] { new PlayerResult(1, won, lpDelta) }, Now, Now);

        Assert.Single(errors);
    }

    [Fact]
    public void Zero_lp_is_allowed_for_a_win_or_a_loss()
    {
        // e.g. a loss protected by a demotion shield
        var results = new[] { new PlayerResult(1, true, 0), new PlayerResult(2, false, 0) };

        Assert.Empty(MatchResultValidator.Validate(results, Now, Now));
    }

    [Fact]
    public void Same_player_twice_in_one_match_is_rejected()
    {
        var results = new[] { new PlayerResult(1, true, 20), new PlayerResult(1, true, 20) };

        var errors = MatchResultValidator.Validate(results, Now, Now);

        Assert.Contains(errors, e => e.Contains("more than once"));
    }

    [Fact]
    public void Unrealistic_lp_change_is_rejected_to_catch_typos()
    {
        var errors = MatchResultValidator.Validate(new[] { new PlayerResult(1, true, 210) }, Now, Now);

        Assert.Contains(errors, e => e.Contains("more than"));
    }

    [Fact]
    public void Match_in_the_future_is_rejected()
    {
        var errors = MatchResultValidator.Validate(new[] { new PlayerResult(1, true, 20) }, Now.AddDays(1), Now);

        Assert.Contains(errors, e => e.Contains("future"));
    }
}
