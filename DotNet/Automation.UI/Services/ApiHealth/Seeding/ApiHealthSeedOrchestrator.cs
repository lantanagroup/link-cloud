using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Sdk.Clients;

namespace Automation.UI.Services.ApiHealth.Seeding;

public enum ApiHealthSeedRequirement
{
    ReportSchedule = 1
}

public sealed class ReportApiHealthSeedData
{
    public string FacilityId { get; init; } = string.Empty;
    public string ScheduleId { get; init; } = string.Empty;
}

public sealed class ApiHealthSeedSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public string Scope { get; init; } = "None";
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Guid? SeedRunId { get; init; }
    public string? SeedRunName { get; init; }
    public ReportApiHealthSeedData? Report { get; init; }
}

public interface IApiHealthSeedContextAccessor
{
    ApiHealthSeedSession? Current { get; set; }
}

public sealed class ApiHealthSeedContextAccessor : IApiHealthSeedContextAccessor
{
    private static readonly AsyncLocal<ApiHealthSeedSession?> _current = new();
    public ApiHealthSeedSession? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

public interface IApiHealthSeedOrchestrator
{
    Task<ApiHealthSeedSession> BeginServiceAsync(string serviceName, IReadOnlyCollection<ApiHealthSeedRequirement> requirements, CancellationToken ct = default);
    Task<ApiHealthSeedSession> BeginAllAsync(IReadOnlyCollection<ApiHealthSeedRequirement> requirements, CancellationToken ct = default);
    Task EndAsync(ApiHealthSeedSession session, CancellationToken ct = default);
}

/// <summary>
/// Phase-B implementation: service-scoped seed orchestration.
/// Currently seeds Report fixture for Report/RunAll and exposes it via AsyncLocal context.
/// </summary>
public sealed class ApiHealthSeedOrchestrator(
    IAutomationRunManager runManager,
    IScenarioStore scenarioStore,
    IFacilityServiceClient facilityClient,
    IApiHealthSeedContextAccessor context,
    ILogger<ApiHealthSeedOrchestrator> logger) : IApiHealthSeedOrchestrator
{
    private static readonly Guid ApiHealthScenarioId = new("00000000-0000-0000-0000-000000000008");
    private const string SanitizedInternalError = "An internal error occurred processing this run.";

    public async Task<ApiHealthSeedSession> BeginServiceAsync(string serviceName, IReadOnlyCollection<ApiHealthSeedRequirement> requirements, CancellationToken ct = default)
    {
        var needsReportSeed = requirements.Contains(ApiHealthSeedRequirement.ReportSchedule);
        var session = needsReportSeed
            ? await BuildReportSeedSessionAsync($"Service:{serviceName}", ct)
            : new ApiHealthSeedSession { Scope = $"Service:{serviceName}", Success = true };

        context.Current = session;
        return session;
    }

    public async Task<ApiHealthSeedSession> BeginAllAsync(IReadOnlyCollection<ApiHealthSeedRequirement> requirements, CancellationToken ct = default)
    {
        var needsReportSeed = requirements.Contains(ApiHealthSeedRequirement.ReportSchedule);
        var session = needsReportSeed
            ? await BuildReportSeedSessionAsync("All", ct)
            : new ApiHealthSeedSession { Scope = "All", Success = true };
        context.Current = session;
        return session;
    }

    public async Task EndAsync(ApiHealthSeedSession session, CancellationToken ct = default)
    {
        try
        {
            if (session.Report is { FacilityId: { Length: > 0 } facilityId })
            {
                try
                {
                    await facilityClient.DeleteAsync(facilityId, ct);
                    logger.LogInformation("API Health seed cleanup removed facility {FacilityId}", facilityId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "API Health seed cleanup failed for facility {FacilityId}", facilityId);
                }
            }
        }
        finally
        {
            context.Current = null;
        }
    }

    private async Task<ApiHealthSeedSession> BuildReportSeedSessionAsync(string scope, CancellationToken ct)
    {
        try
        {
            var scenario = await scenarioStore.GetByIdAsync(ApiHealthScenarioId, ct);
            if (scenario == null)
            {
                scenario = (await scenarioStore.GetAllAsync(ct))
                    .FirstOrDefault(s => s.Name.Equals("ApiHealthScenario", StringComparison.OrdinalIgnoreCase));
                if (scenario == null)
                {
                    return new ApiHealthSeedSession
                    {
                        Scope = scope,
                        Success = false,
                        Error = "ApiHealthScenario was not found. Seed scenario must exist as a system scenario."
                    };
                }
            }

            var startRequest = StartScenarioRequest.FromScenario(scenario);
            var runId = await runManager.StartAsync(startRequest, ct);

            logger.LogInformation(
                "API Health seeding started via automation scenario. Scope={Scope}, ScenarioId={ScenarioId}, ScenarioName={ScenarioName}, RunId={RunId}",
                scope,
                scenario.Id,
                scenario.Name,
                runId);

            const int maxPollAttempts = 900; // ~30 minutes at 2s polling.
            for (var i = 0; i < maxPollAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(2), ct);

                var run = await runManager.GetRunAsync(runId, ct);
                if (run == null)
                    continue;

                var isTerminal = run.Status is AutomationRunStatus.Succeeded
                    or AutomationRunStatus.Failed
                    or AutomationRunStatus.Cancelled;

                if (!isTerminal)
                    continue;

                if (run.Status != AutomationRunStatus.Succeeded)
                {
                    logger.LogWarning(
                        "ApiHealthScenario seed run failed. Scope={Scope}, RunId={RunId}, Status={Status}, Error={RunError}",
                        scope,
                        runId,
                        run.Status,
                        run.Error);

                    return new ApiHealthSeedSession
                    {
                        Scope = scope,
                        Success = false,
                        SeedRunId = runId,
                        SeedRunName = scenario.Name,
                        Error = SanitizedInternalError
                    };
                }

                if (string.IsNullOrWhiteSpace(run.FacilityId) || string.IsNullOrWhiteSpace(run.ReportId))
                {
                    return new ApiHealthSeedSession
                    {
                        Scope = scope,
                        Success = false,
                        SeedRunId = runId,
                        SeedRunName = scenario.Name,
                        Error = $"ApiHealthScenario run {runId} succeeded but did not produce facility/report identifiers."
                    };
                }

                logger.LogInformation(
                    "API Health seed scenario run completed. Scope={Scope}, RunId={RunId}, FacilityId={FacilityId}, ReportId={ReportId}",
                    scope,
                    runId,
                    run.FacilityId,
                    run.ReportId);

                return new ApiHealthSeedSession
                {
                    Scope = scope,
                    Success = true,
                    SeedRunId = runId,
                    SeedRunName = scenario.Name,
                    Report = new ReportApiHealthSeedData
                    {
                        FacilityId = run.FacilityId,
                        ScheduleId = run.ReportId
                    }
                };
            }

            return new ApiHealthSeedSession
            {
                Scope = scope,
                Success = false,
                SeedRunName = scenario.Name,
                Error = "ApiHealthScenario run timed out while waiting for completion."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while building API Health seed session. Scope={Scope}", scope);
            return new ApiHealthSeedSession
            {
                Scope = scope,
                Success = false,
                Error = SanitizedInternalError
            };
        }
    }

}
