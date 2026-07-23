using LantanaGroup.Link.Automation.Link.Helpers;
using System.Text.RegularExpressions;

namespace Automation.UI.Services;

/// <summary>
/// Runs a run's validators, recording each outcome and collecting failures so that every validator
/// executes before the run is failed.
/// <para>
/// Failing on the first validator hid the evidence needed to localise a discrepancy: the ABS manifest
/// validator runs first, so when it reported a missing resource the DataAcquisition and Normalization
/// validators never executed, and there was no way to tell whether the resource had been lost upstream
/// of aggregation or by it. The layers are only diagnostic when read together.
/// </para>
/// </summary>
public sealed class ValidatorRunner
{
    /// <summary>Matches the conventional validator message format "... failed with N issue(s)."</summary>
    private static readonly Regex IssueCountPattern = new(@"(\d+)\s+issue\(s\)", RegexOptions.Compiled);

    private readonly List<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot> _results = new();
    private readonly List<string> _failures = new();
    private readonly Func<IReadOnlyList<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot>, CancellationToken, Task>? _persistAsync;

    /// <param name="persistAsync">
    /// Invoked after each validator so partial results stay visible in the dashboard even when a later
    /// validator fails. Optional so the runner can be exercised without a snapshot store.
    /// </param>
    public ValidatorRunner(
        Func<IReadOnlyList<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot>, CancellationToken, Task>? persistAsync = null)
    {
        _persistAsync = persistAsync;
    }

    public IReadOnlyList<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot> Results => _results;

    public IReadOnlyList<string> Failures => _failures;

    /// <summary>
    /// Runs one validator. A failure is recorded and returned from, never thrown, so the validators
    /// that follow still run.
    /// </summary>
    public async Task RunAsync(string name, Func<Task> action, CancellationToken cancellationToken = default)
    {
        try
        {
            await action();
            _results.Add(new PipelineSummarySnapshotBuilder.ValidatorResultSnapshot
            {
                Name = name,
                Outcome = "Passed",
                IssueCount = 0
            });
        }
        catch (Exception ex)
        {
            var issueCount = 0;
            var match = IssueCountPattern.Match(ex.Message);
            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value, out issueCount);
            }

            _results.Add(new PipelineSummarySnapshotBuilder.ValidatorResultSnapshot
            {
                Name = name,
                Outcome = "Failed",
                IssueCount = issueCount
            });

            _failures.Add($"{name}: {ex.Message}");
        }
        finally
        {
            if (_persistAsync != null)
            {
                await _persistAsync(_results, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Fails the run with every collected failure listed, or returns quietly when all validators passed.
    /// </summary>
    public void ThrowIfAnyFailed()
    {
        if (_failures.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{_failures.Count} validator(s) failed:{Environment.NewLine}" +
            string.Join(Environment.NewLine, _failures.Select(failure => "  - " + failure)));
    }
}
