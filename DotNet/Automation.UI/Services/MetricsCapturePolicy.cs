namespace Automation.UI.Services;

/// <summary>
/// Performance history is for comparable timings, not every failed attempt.
/// </summary>
public static class MetricsCapturePolicy
{
    /// <summary>
    /// Capture when this is a metrics run and the Automation validator suite
    /// passed. Duration-budget failures happen after that point and stay in
    /// history. Validator or pipeline failures do not.
    /// </summary>
    public static bool ShouldCapture(bool isMetricsRun, bool validatorsPassed)
        => isMetricsRun && validatorsPassed;
}
