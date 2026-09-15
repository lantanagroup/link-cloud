using Automation.UI.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

/// <summary>
/// Covers the collect-then-throw behaviour of the E2E scenario runner's validator loop. Failing on the
/// first validator destroyed the evidence needed to localise a discrepancy — the ABS manifest validator
/// runs first, so a missing-resource report from it meant the DataAcquisition and Normalization
/// validators never ran, and there was no way to tell whether the resource was lost upstream of
/// aggregation or by it.
/// </summary>
[Trait("Category", "UnitTests")]
public class ValidatorRunnerTests
{
    private static Task Passing() => Task.CompletedTask;

    private static Func<Task> Failing(string message) => () => throw new InvalidOperationException(message);

    [Fact]
    public async Task EveryValidatorRunsEvenAfterOneFails()
    {
        // The point of the change: a failure must not stop the validators that follow, because their
        // results are what localise the failure.
        var runner = new ValidatorRunner();
        var executed = new List<string>();

        await runner.RunAsync("FIRST", () => { executed.Add("FIRST"); return Failing("boom")(); });
        await runner.RunAsync("SECOND", () => { executed.Add("SECOND"); return Passing(); });
        await runner.RunAsync("THIRD", () => { executed.Add("THIRD"); return Passing(); });

        Assert.Equal(new[] { "FIRST", "SECOND", "THIRD" }, executed);
    }

    [Fact]
    public async Task MultipleFailuresThrowASingleAggregateListingAllOfThem()
    {
        var runner = new ValidatorRunner();

        await runner.RunAsync("ABS MANIFEST", Failing("failed with 2 issue(s): missing Observation-030"));
        await runner.RunAsync("REPORT DATABASE", Passing);
        await runner.RunAsync("DATA ACQUISITION", Failing("failed with 1 issue(s): count mismatch"));

        var exception = Assert.Throws<InvalidOperationException>(() => runner.ThrowIfAnyFailed());

        Assert.Contains("2 validator(s) failed", exception.Message);
        Assert.Contains("ABS MANIFEST: failed with 2 issue(s): missing Observation-030", exception.Message);
        Assert.Contains("DATA ACQUISITION: failed with 1 issue(s): count mismatch", exception.Message);
        Assert.DoesNotContain("REPORT DATABASE", exception.Message);
    }

    [Fact]
    public async Task NoFailuresDoesNotThrow()
    {
        // The passing path has to stay silent, or every successful run would fail at cleanup.
        var runner = new ValidatorRunner();

        await runner.RunAsync("ABS MANIFEST", Passing);
        await runner.RunAsync("REPORT DATABASE", Passing);

        runner.ThrowIfAnyFailed();

        Assert.Empty(runner.Failures);
        Assert.All(runner.Results, result => Assert.Equal("Passed", result.Outcome));
    }

    [Fact]
    public async Task RecordsOutcomeAndParsedIssueCountPerValidator()
    {
        var runner = new ValidatorRunner();

        await runner.RunAsync("ABS MANIFEST", Failing("REPORT INTERNAL ABS MANIFEST VALIDATION failed with 2 issue(s): ..."));
        await runner.RunAsync("REPORT DATABASE", Passing);

        Assert.Equal(2, runner.Results.Count);

        var failed = runner.Results.Single(r => r.Name == "ABS MANIFEST");
        Assert.Equal("Failed", failed.Outcome);
        Assert.Equal(2, failed.IssueCount);

        var passed = runner.Results.Single(r => r.Name == "REPORT DATABASE");
        Assert.Equal("Passed", passed.Outcome);
        Assert.Equal(0, passed.IssueCount);
    }

    [Fact]
    public async Task IssueCountIsZeroWhenTheMessageDoesNotCarryOne()
    {
        // Not every failure is a validator assertion — a timeout or connection error has no issue
        // count, and must not be misreported as one.
        var runner = new ValidatorRunner();

        await runner.RunAsync("TENANT DATABASE", Failing("Connection refused"));

        Assert.Equal(0, runner.Results.Single().IssueCount);
        Assert.Equal("Failed", runner.Results.Single().Outcome);
    }

    [Fact]
    public async Task ResultsArePersistedAfterEachValidatorIncludingFailures()
    {
        // Partial results have to reach the dashboard as they happen; persisting only at the end would
        // lose them precisely when a run fails.
        var persistedCounts = new List<int>();

        var runner = new ValidatorRunner((results, _) =>
        {
            persistedCounts.Add(results.Count);
            return Task.CompletedTask;
        });

        await runner.RunAsync("FIRST", Failing("failed with 1 issue(s)"));
        await runner.RunAsync("SECOND", Passing);

        Assert.Equal(new[] { 1, 2 }, persistedCounts);
    }

    [Fact]
    public async Task CancellationPropagatesInsteadOfBeingRecordedAsAFailure()
    {
        // A cancelled run must not be reported as a failed one. Without this, every remaining
        // validator would throw, each would be recorded as Failed, and the run would surface as
        // "N validator(s) failed" rather than cancelled.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var runner = new ValidatorRunner();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync("ABS MANIFEST", () => throw new OperationCanceledException(cts.Token), cts.Token));

        Assert.Empty(runner.Failures);
        Assert.Empty(runner.Results);

        // And nothing was collected, so the run does not additionally fail validation.
        runner.ThrowIfAnyFailed();
    }

    [Fact]
    public async Task CancellationExceptionWithoutCancellationRequestedIsStillAFailure()
    {
        // A timeout surfaces as TaskCanceledException while no cancellation was requested. That is a
        // genuine validator failure and must not be mistaken for the run being cancelled.
        var runner = new ValidatorRunner();

        await runner.RunAsync("TENANT DATABASE", () => throw new TaskCanceledException("HTTP timeout"));

        Assert.Single(runner.Failures);
        Assert.Contains("TENANT DATABASE: HTTP timeout", runner.Failures.Single());
        Assert.Equal("Failed", runner.Results.Single().Outcome);
    }

    [Fact]
    public async Task PersistIsSkippedOnceCancellationIsRequested()
    {
        // An exception thrown from the finally block would replace the propagating
        // OperationCanceledException, masking the cancellation.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var persistCalled = false;
        var runner = new ValidatorRunner((_, _) =>
        {
            persistCalled = true;
            throw new InvalidOperationException("snapshot store rejected a cancelled write");
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync("ABS MANIFEST", () => throw new OperationCanceledException(cts.Token), cts.Token));

        Assert.False(persistCalled);
    }

    [Fact]
    public async Task PassesTheCancellationTokenToThePersistCallback()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken observed = default;

        var runner = new ValidatorRunner((_, token) =>
        {
            observed = token;
            return Task.CompletedTask;
        });

        await runner.RunAsync("FIRST", Passing, cts.Token);

        Assert.Equal(cts.Token, observed);
    }
}
