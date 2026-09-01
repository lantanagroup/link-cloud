using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Centralised post-run cleanup logic.
/// Both the Automation UI and the backend E2E tests call through here
/// so cleanup behaviour stays consistent across consumers.
/// </summary>
public static class RunCleanupHelper
{
    /// <summary>
    /// Runs all post-run cleanup steps based on the flags in <paramref name="config"/>.
    /// </summary>
    public static async Task CleanupAfterRunAsync(
        TestScenarioConfig config,
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IReportServiceClient reportClient,
        FhirDataLoader fhirDataLoader,
        IAutomationOutput output,
        string facilityId,
        string? reportId)
    {
        if (config.CleanupServiceData)
        {
            await FacilitySetupHelper.CleanupFacilityAsync(
                facilityClient, normalizationClient, dataAcqClient, queryDispatchClient,
                output, facilityId);

            await FacilitySetupHelper.SoftDeleteRunDataAsync(
                reportClient, dataAcqClient, queryDispatchClient,
                output, facilityId, reportId ?? string.Empty);
        }

        if (config.CleanupFhirData)
        {
            fhirDataLoader.DeleteResourcesWithExpunge(output);
        }
    }

    /// <summary>
    /// Stops work that is still moving: abort Kafka consumers, cancel DA retries,
    /// disable census jobs, deactivate the report schedule. Leaves facility configs
    /// and cancelled logs in place for debug until teardown.
    /// </summary>
    public static async Task AbortAndQuiesceFacilityAsync(
        IPipelineAbortRegistry? abortRegistry,
        IDataAcquisitionServiceClient dataAcqClient,
        ICensusServiceClient censusClient,
        IReportServiceClient reportClient,
        IAutomationOutput output,
        string facilityId,
        string? reportId,
        TimeSpan abortTtl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return;

        if (abortRegistry != null)
        {
            try
            {
                await abortRegistry.AbortAsync(facilityId, reportId, abortTtl, cancellationToken);
                output.WriteLine($"Marked pipeline aborted for '{facilityId}'.");
            }
            catch (Exception ex)
            {
                output.WriteLine($"Warning: pipeline abort flag failed for '{facilityId}': {ex.Message}");
            }
        }

        try
        {
            var cancelResult = await dataAcqClient.CancelAcquisitionLogsByFilterAsync(
                new { FacilityId = facilityId },
                minAgeHours: 0,
                cancellationToken);
            output.WriteLine($"Cancelled leftover DA work for '{facilityId}' (cancelled={cancelResult?.Body?.Cancelled ?? 0}).");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: DA cancel failed for '{facilityId}': {ex.Message}");
        }

        try
        {
            await censusClient.DisableFacilityJobsAsync(facilityId, cancellationToken);
            output.WriteLine($"Disabled census jobs for '{facilityId}'.");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: census disable failed for '{facilityId}': {ex.Message}");
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(reportId))
                await reportClient.SoftDeleteScheduleAsync(reportId, cancellationToken);
            await reportClient.SetReportsDeletedStatusForFacilityAsync(facilityId, deleted: true, cancellationToken);
            output.WriteLine($"Deactivated report schedules for '{facilityId}'.");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: report schedule deactivate failed for '{facilityId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Cancellation: abort and quiesce immediately, expunge FHIR so the server
    /// does not keep mega-patient volume, but leave facility configs for the 14-day tail.
    /// </summary>
    public static async Task CleanupCancelledRunAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        ICensusServiceClient censusClient,
        IReportServiceClient reportClient,
        IPipelineAbortRegistry? abortRegistry,
        FhirDataLoader fhirDataLoader,
        IAutomationOutput output,
        string? facilityId,
        string? reportId,
        TimeSpan abortTtl,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            await AbortAndQuiesceFacilityAsync(
                abortRegistry,
                dataAcqClient,
                censusClient,
                reportClient,
                output,
                facilityId,
                reportId,
                abortTtl,
                cancellationToken);
        }

        try
        {
            fhirDataLoader.DeleteResourcesWithExpunge(output);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: FHIR expunge failed during cancel cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Tears down a leftover Automation facility without expunging the whole FHIR server.
    /// Used by the leftover sweeper so old GUID facilities stop driving DA census reads.
    /// </summary>
    public static async Task CleanupLeftoverFacilityAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        ICensusServiceClient censusClient,
        IReportServiceClient reportClient,
        IPipelineAbortRegistry? abortRegistry,
        IAutomationOutput output,
        string facilityId,
        TimeSpan abortTtl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return;

        await AbortAndQuiesceFacilityAsync(
            abortRegistry,
            dataAcqClient,
            censusClient,
            reportClient,
            output,
            facilityId,
            reportId: null,
            abortTtl,
            cancellationToken);

        try
        {
            await dataAcqClient.SoftDeleteLogsByFacilityAsync(facilityId, cancellationToken);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: leftover DA log soft-delete failed for '{facilityId}': {ex.Message}");
        }

        try
        {
            await censusClient.DeleteCensusConfigAsync(facilityId, cancellationToken);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: leftover census config delete failed for '{facilityId}': {ex.Message}");
        }

        try
        {
            await FacilitySetupHelper.CleanupFacilityAsync(
                facilityClient,
                normalizationClient,
                dataAcqClient,
                queryDispatchClient,
                output,
                facilityId);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: leftover facility teardown failed for '{facilityId}': {ex.Message}");
        }
    }

    public static bool IsAutomationFacilityId(string? facilityId) =>
        Guid.TryParse(facilityId, out _);

    /// <summary>
    /// GUID facilities whose run is terminal past <paramref name="grace"/>, plus GUID
    /// facilities with no matching run. These still have configs; only hot work is stopped.
    /// </summary>
    public static IReadOnlyList<string> SelectQuiesceAutomationFacilities(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset now,
        TimeSpan grace)
        => SelectAutomationFacilities(facilities, runs, now, grace);

    /// <summary>
    /// GUID facilities whose run has been terminal longer than <paramref name="retention"/>,
    /// plus GUID orphans with no run record.
    /// </summary>
    public static IReadOnlyList<string> SelectTeardownAutomationFacilities(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset now,
        TimeSpan retention)
        => SelectAutomationFacilities(facilities, runs, now, retention);

    public static IReadOnlyList<string> SelectLeftoverAutomationFacilities(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset now,
        TimeSpan retention)
        => SelectTeardownAutomationFacilities(facilities, runs, now, retention);

    /// <summary>
    /// GUID facilities whose Automation run is still non-terminal but older than
    /// <paramref name="retention"/>. Daily teardown aborts these as leftover ongoing jobs.
    /// </summary>
    public static IReadOnlyList<string> SelectStaleActiveAutomationFacilities(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset now,
        TimeSpan retention)
    {
        var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs)
        {
            if (run.Status.IsTerminal())
                continue;

            if (now - RunTimestamp(run) < retention)
                continue;

            if (IsAutomationFacilityId(run.FacilityId))
                stale.Add(run.FacilityId!);
            var runId = run.RunId.ToString();
            if (IsAutomationFacilityId(runId))
                stale.Add(runId);
        }

        return facilities.Keys
            .Where(IsAutomationFacilityId)
            .Where(stale.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<AutomationRunSummary> SelectHistoryPurgeRuns(
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset now,
        TimeSpan retention)
        => runs.Where(run => now - RunTimestamp(run) >= retention).ToList();

    public static IReadOnlyList<AutomationRunSummary> SelectRunsFinishedInRange(
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset fromInclusiveUtc,
        DateTimeOffset toExclusiveUtc)
        => runs
            .Where(run => run.Status.IsTerminal())
            .Where(run =>
            {
                var stamp = RunTimestamp(run);
                return stamp >= fromInclusiveUtc && stamp < toExclusiveUtc;
            })
            .ToList();

    public static IReadOnlyList<string> SelectAutomationFacilitiesForRuns(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs)
        {
            if (IsAutomationFacilityId(run.FacilityId))
                ids.Add(run.FacilityId!);
            var runId = run.RunId.ToString();
            if (IsAutomationFacilityId(runId))
                ids.Add(runId);
        }

        return facilities.Keys
            .Where(IsAutomationFacilityId)
            .Where(ids.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static DateTimeOffset RunTimestamp(AutomationRunSummary run)
        => run.FinishedAt ?? run.StartedAt ?? run.CreatedAt;

    private static IReadOnlyList<string> SelectAutomationFacilities(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        DateTimeOffset now,
        TimeSpan minAge)
    {
        var protectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs)
        {
            if (!run.Status.IsTerminal())
            {
                Protect(protectedIds, run);
                continue;
            }

            var finished = run.FinishedAt ?? run.StartedAt ?? run.CreatedAt;
            if (now - finished < minAge)
                Protect(protectedIds, run);
        }

        return facilities.Keys
            .Where(IsAutomationFacilityId)
            .Where(id => !protectedIds.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Protect(HashSet<string> protectedIds, AutomationRunSummary run)
    {
        protectedIds.Add(run.RunId.ToString());
        if (!string.IsNullOrWhiteSpace(run.FacilityId))
            protectedIds.Add(run.FacilityId);
    }
}
