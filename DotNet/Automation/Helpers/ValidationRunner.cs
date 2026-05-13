namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// A single validation step in a multi-step validation pipeline.
/// Platform implementations create concrete steps for their domain
/// (e.g., database validation, API validation, log validation).
/// </summary>
public interface IValidationStep
{
    /// <summary>
    /// Human-readable name of this validation step (e.g., "Report Database Validation").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the validation and returns a result.
    /// Implementations should NOT throw — capture all errors in the result.
    /// </summary>
    Task<ValidationStepResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a single validation step execution.
/// </summary>
public sealed class ValidationStepResult
{
    public string StepName { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public TimeSpan Duration { get; init; }

    public static ValidationStepResult Pass(string stepName, TimeSpan duration) => new()
    {
        StepName = stepName,
        Passed = true,
        Duration = duration
    };

    public static ValidationStepResult Fail(string stepName, List<string> errors, TimeSpan duration, List<string>? warnings = null) => new()
    {
        StepName = stepName,
        Passed = false,
        Errors = errors,
        Warnings = warnings ?? [],
        Duration = duration
    };
}

/// <summary>
/// Runs a collection of <see cref="IValidationStep"/>s in sequence
/// and produces a summary report.
/// </summary>
public sealed class ValidationRunner
{
    private readonly IAutomationOutput _output;
    private readonly List<IValidationStep> _steps;

    public ValidationRunner(IAutomationOutput output, IEnumerable<IValidationStep> steps)
    {
        _output = output;
        _steps = steps.ToList();
    }

    public ValidationRunner(IAutomationOutput output, params IValidationStep[] steps)
        : this(output, steps.AsEnumerable())
    {
    }

    /// <summary>
    /// Runs all registered steps and returns the aggregate results.
    /// If <paramref name="stopOnFirstFailure"/> is true, stops after the first failed step.
    /// </summary>
    public async Task<ValidationRunResult> RunAllAsync(
        bool stopOnFirstFailure = false,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ValidationStepResult>();
        var overallStart = DateTime.UtcNow;

        _output.WriteLine($"Running {_steps.Count} validation step(s)...");

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _output.WriteLine($"  [{step.Name}] Starting...");
            var stepStart = DateTime.UtcNow;

            ValidationStepResult result;
            try
            {
                result = await step.ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result = ValidationStepResult.Fail(
                    step.Name,
                    [$"Unhandled exception: {ex.Message}"],
                    DateTime.UtcNow - stepStart);
            }

            results.Add(result);

            if (result.Passed)
            {
                _output.WriteLine($"  [{step.Name}] PASSED ({result.Duration.TotalSeconds:F1}s)");
            }
            else
            {
                _output.WriteLine($"  [{step.Name}] FAILED ({result.Duration.TotalSeconds:F1}s) — {result.Errors.Count} error(s)");
                foreach (var error in result.Errors.Take(10))
                    _output.WriteLine($"    - {error}");
                if (result.Errors.Count > 10)
                    _output.WriteLine($"    - ... and {result.Errors.Count - 10} more");
            }

            if (result.Warnings.Count > 0)
            {
                foreach (var warning in result.Warnings.Take(5))
                    _output.WriteLine($"    ⚠ {warning}");
            }

            if (!result.Passed && stopOnFirstFailure)
            {
                _output.WriteLine("  Stopping validation run due to failure.");
                break;
            }
        }

        var totalDuration = DateTime.UtcNow - overallStart;
        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => !r.Passed);

        _output.WriteLine($"Validation complete: {passed} passed, {failed} failed ({totalDuration.TotalSeconds:F1}s total).");

        return new ValidationRunResult
        {
            Steps = results,
            TotalDuration = totalDuration
        };
    }
}

/// <summary>
/// Aggregate result of a full validation run.
/// </summary>
public sealed class ValidationRunResult
{
    public List<ValidationStepResult> Steps { get; init; } = [];
    public TimeSpan TotalDuration { get; init; }
    public bool AllPassed => Steps.All(s => s.Passed);
    public int PassedCount => Steps.Count(s => s.Passed);
    public int FailedCount => Steps.Count(s => !s.Passed);
    public List<string> AllErrors => Steps.SelectMany(s => s.Errors).ToList();

    /// <summary>
    /// Throws if any step failed, with a summary of all errors.
    /// </summary>
    public void ThrowIfFailed()
    {
        if (AllPassed) return;

        var summary = string.Join("\n",
            Steps.Where(s => !s.Passed)
                 .Select(s => $"[{s.StepName}]: {string.Join("; ", s.Errors.Take(3))}"));

        throw new InvalidOperationException(
            $"Validation failed ({FailedCount} step(s)):\n{summary}");
    }
}
