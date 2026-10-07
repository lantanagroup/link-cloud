using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services;

[Authorize]
public class CleanupHub : Hub
{
    public const string Group = "cleanup";

    public Task SubscribeCleanup()
        => Groups.AddToGroupAsync(Context.ConnectionId, Group);
}

public sealed record CleanupActivity
{
    public static readonly CleanupActivity Idle = new()
    {
        Mode = "",
        Label = "Idle",
        Status = "idle",
        Trigger = "",
        At = DateTimeOffset.MinValue
    };

    public required string Mode { get; init; }
    public required string Label { get; init; }
    public required string Status { get; init; }
    public required string Trigger { get; init; }
    public int Total { get; init; }
    public int Processed { get; init; }
    public int Quiesced { get; init; }
    public int TornDown { get; init; }
    public int Purged { get; init; }
    public int Failed { get; init; }
    public string? CurrentItem { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset At { get; init; }

    public int Percent => Total <= 0 ? (Status is "completed" or "failed" ? 100 : 0)
        : Math.Clamp((int)Math.Round(100.0 * Processed / Total), 0, 100);
}

public interface ILeftoverRunCleanup
{
    DateTimeOffset? LastQuiesceAt { get; }
    LeftoverCleanupResult? LastQuiesceResult { get; }
    CleanupActivity CurrentActivity { get; }
    bool IsRunning { get; }
    void StartQuiesceInBackground();
    void StartTeardownInBackground();
    void StartHistoryPurgeInBackground();
    void StartCustomRangeInBackground(
        DateTimeOffset fromInclusiveUtc,
        DateTimeOffset toExclusiveUtc,
        bool teardownFacilities,
        bool purgeHistory);
}
