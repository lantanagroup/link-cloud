using LantanaGroup.Link.Automation.Link.Helpers;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class DiagnosticsEventWatcher : IAsyncDisposable
{
    private readonly IAutomationOutput _output;
    private readonly BackgroundDiagnosticsMonitor _diagnostics;
    private readonly CancellationTokenSource _cts;
    private readonly Task _pumpTask;
    private readonly bool _writeEventsToOutput;

    private static readonly HashSet<AutomationMonitorEventType> SuppressedEventTypes =
    [
        AutomationMonitorEventType.CycleStarted,
        AutomationMonitorEventType.MilestoneReached
    ];

    public int CriticalEventCount { get; private set; }

    private DiagnosticsEventWatcher(
        BackgroundDiagnosticsMonitor diagnostics,
        IAutomationOutput output,
        bool writeEventsToOutput)
    {
        _diagnostics = diagnostics;
        _output = output;
        _writeEventsToOutput = writeEventsToOutput;
        _cts = new CancellationTokenSource();
        _pumpTask = Task.Run(PumpAsync);
    }

    public static DiagnosticsEventWatcher Start(
        BackgroundDiagnosticsMonitor diagnostics,
        IAutomationOutput output,
        bool writeEventsToOutput = true)
        => new(diagnostics, output, writeEventsToOutput);

    public async Task StopAsync()
    {
        await _cts.CancelAsync();

        try
        {
            await _pumpTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }

    private async Task PumpAsync()
    {
        await foreach (var evt in _diagnostics.StreamEventsAsync(_cts.Token))
        {
            if (_writeEventsToOutput && !SuppressedEventTypes.Contains(evt.Type))
            {
                WriteEvent(evt);
            }

            if (evt.Type == AutomationMonitorEventType.CriticalFailureDetected || evt.Severity == MonitorIssueSeverity.Critical)
            {
                CriticalEventCount++;
            }
        }
    }

    private void WriteEvent(AutomationMonitorEvent evt)
    {
        if (evt.Type == AutomationMonitorEventType.LogMessage)
        {
            _output.WriteLine(evt.Message);
            return;
        }

        var level = evt.Severity switch
        {
            MonitorIssueSeverity.Critical => "CRITICAL",
            MonitorIssueSeverity.Warning => "WARN",
            _ => "INFO"
        };

        _output.WriteLine($"[DIAG][{level}] {evt.Type}: {evt.Message}");
    }
}
