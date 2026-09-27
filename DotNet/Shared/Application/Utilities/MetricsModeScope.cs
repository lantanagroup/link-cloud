namespace LantanaGroup.Link.Shared.Application.Utilities;

/// <summary>
/// Same-process hint that the current consume or work item saw X-Metrics-Mode=performance.
/// Missing/unknown header is lightweight. Does not cross process or survive Quartz hops.
/// </summary>
public static class MetricsModeScope
{
    private static readonly AsyncLocal<bool> Performance = new();

    public static bool IsPerformance => Performance.Value;

    public static IDisposable Begin(bool isPerformance)
    {
        var previous = Performance.Value;
        Performance.Value = isPerformance;
        return new Popper(previous);
    }

    private sealed class Popper(bool previous) : IDisposable
    {
        public void Dispose() => Performance.Value = previous;
    }
}
