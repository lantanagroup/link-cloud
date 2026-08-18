using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services;

/// <summary>
/// TEMPORARY / DEV-ONLY: seeds the Automation.UI snapshot store with synthetic
/// "old" runs going back 30 days (1–2 runs per day) so the dashboard can be
/// exercised end-to-end — KPIs, status pie, 14-day histogram, Recent Runs table —
/// without having to wait for a month of real runs to accumulate.
///
/// Removal plan (once the dashboard verification is done):
///   1. Delete this file.
///   2. Remove the <c>AddHostedService&lt;DashboardSeedService&gt;()</c> line
///      from <c>Program.cs</c>.
///   3. Remove the <c>Dashboard:SeedFakeRuns</c> key from appsettings (if any).
///   4. Wipe the seeded rows once from Mongo:
///        db.automation_runs.deleteMany({ RunName: { $regex: "^SEED " } })
///      All seeded documents use the <see cref="SeedRunNamePrefix"/> so this is
///      a safe, one-line cleanup that cannot touch real runs.
///
/// Behavior:
///   • Only runs when <c>Dashboard:SeedFakeRuns</c> is true (default false), so
///     this service is a no-op in any environment where the flag isn't set.
///     The flag is gated at registration time in <c>Program.cs</c> — when it's
///     false the service isn't registered at all and no Mongo activity occurs.
///   • Note: the flag is read from compiled-in <c>appsettings.json</c> in
///     containerized deployments. Editing the source file without rebuilding
///     the image leaves the container running with whatever value was baked
///     in at build time. If you flip the flag in source and the running app
///     still seeds (or the running app still won't seed), rebuild the image.
///   • Uses deterministic GUIDs derived from (date, slot) so re-running on
///     subsequent boots upserts the same rows rather than piling on duplicates.
///     That means the seeded dataset stays at ~60 rows indefinitely.
///   • Every row is marked with a distinctive <see cref="SeedRunNamePrefix"/>
///     and FacilityId/ReportId so a human can tell it apart from real data at
///     a glance in the Recent Runs table.
///   • Status distribution is mixed so the status-breakdown donut and the
///     per-day stacked bar both have something non-trivial to render.
/// </summary>
public sealed class DashboardSeedService : IHostedService
{
    /// <summary>All seeded RunName values start with this token so cleanup is a
    /// single regex-based <c>deleteMany</c> in the Mongo shell.</summary>
    public const string SeedRunNamePrefix = "SEED ";

    private const int DaysBack = 30;
    private const int RunsPerDayMin = 1;
    private const int RunsPerDayMax = 2;

    private readonly ISnapshotStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<DashboardSeedService> _logger;

    public DashboardSeedService(
        ISnapshotStore store,
        IConfiguration config,
        ILogger<DashboardSeedService> logger)
    {
        _store = store;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Deterministic RNG per-boot: seeded from today's date so the status
        // distribution is stable across restarts on the same day (makes visual
        // verification of the dashboard chart less jittery) but shifts naturally
        // as the 30-day window rolls forward.
        var rng = new Random(DateTime.UtcNow.Date.GetHashCode());

        var today = DateTime.UtcNow.Date;
        var upserted = 0;

        for (var daysAgo = 0; daysAgo < DaysBack; daysAgo++)
        {
            var day = today.AddDays(-daysAgo);
            var runsToday = rng.Next(RunsPerDayMin, RunsPerDayMax + 1);

            for (var slot = 0; slot < runsToday; slot++)
            {
                var summary = BuildSeedRun(day, slot, rng);
                try
                {
                    await _store.UpsertRunSummaryAsync(
                        summary,
                        summary.FacilityId,
                        summary.ReportId,
                        cancellationToken);
                    upserted++;
                }
                catch (Exception ex)
                {
                    // Keep seeding the rest of the window even if one row fails;
                    // this is dev telemetry, not a data-integrity concern.
                    _logger.LogWarning(ex,
                        "DashboardSeedService: failed to upsert seed run for {Day} slot {Slot}.",
                        day, slot);
                }
            }
        }

        _logger.LogInformation(
            "DashboardSeedService: upserted {Count} synthetic runs spanning the last {Days} days. " +
            "Remove with: db.automation_runs.deleteMany({{ RunName: {{ $regex: '^{Prefix}' }} }})",
            upserted, DaysBack, SeedRunNamePrefix);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static AutomationRunSummary BuildSeedRun(DateTime day, int slot, Random rng)
    {
        // Spread the two slots across the day so the histogram isn't visually
        // stacked at midnight; hour-of-day doesn't affect any dashboard math,
        // only the "Created" column in the Recent Runs table.
        var hour = slot == 0 ? 9 : 15;
        var createdAt = new DateTimeOffset(day.AddHours(hour).AddMinutes(rng.Next(0, 60)), TimeSpan.Zero);

        // Duration between 30s and 5m — long enough to feel real on the Avg
        // Duration KPI, short enough that the "Duration" column fits neatly.
        var durationSeconds = rng.Next(30, 301);
        var finishedAt = createdAt.AddSeconds(durationSeconds);

        // Status mix ≈ 70% Succeeded / 20% Failed / 10% Cancelled. Tuned so the
        // Success Rate KPI sits in the yellow-to-green band rather than 100%
        // (which would hide the conditional styling) or 0%.
        var roll = rng.Next(100);
        var status = roll < 70 ? AutomationRunStatus.Succeeded
                   : roll < 90 ? AutomationRunStatus.Failed
                               : AutomationRunStatus.Cancelled;

        return new AutomationRunSummary
        {
            RunId = DeterministicRunId(day, slot),
            RunName = $"{SeedRunNamePrefix}{day:yyyy-MM-dd} #{slot + 1}",
            Scenario = AutomationScenarioKind.Custom,
            SelectedMeasure = string.Empty,
            PatientCount = rng.Next(5, 25),
            ResourcesPerPatient = 250,
            Seed = 20260329,
            Status = status,
            CreatedAt = createdAt,
            StartedAt = createdAt,
            FinishedAt = finishedAt,
            Duration = FormatDuration(durationSeconds),
            // Distinctive FacilityId/ReportId so if a dev clicks into Details by
            // accident it's obvious this isn't a real pipeline artifact.
            FacilityId = $"seed-facility-{day:yyyyMMdd}-{slot}",
            ReportId = $"seed-report-{day:yyyyMMdd}-{slot}",
            Error = status == AutomationRunStatus.Failed ? "Synthetic failure for dashboard seeding." : null,
        };
    }

    /// <summary>
    /// Derives a stable GUID from (date, slot). Same (date, slot) always yields
    /// the same GUID, so <c>UpsertRunSummaryAsync</c> overwrites the existing
    /// row on every boot instead of creating new ones.
    /// </summary>
    private static Guid DeterministicRunId(DateTime day, int slot)
    {
        // MD5 is used here purely as a 16-byte hash for Guid construction — not
        // for security. The input is a short ASCII string so the cost is trivial.
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes($"dashboard-seed::{day:yyyyMMdd}::{slot}"));
        return new Guid(bytes);
    }

    private static string FormatDuration(int totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.Seconds}s";
    }
}
