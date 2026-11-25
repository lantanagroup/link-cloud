using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// One cached read of the home cards. Each card uses a call the matching page already makes.
/// A card that does not answer is an empty state. The others still load.
/// </summary>
public interface IHomeOverview
{
    Task<HomeOverviewModel> LoadAsync(CancellationToken cancellationToken);
}

public sealed class HomeOverviewService : IHomeOverview
{
    public const string CacheKey = "link-ui:home-overview";

    private readonly IFacilityServiceClient _facilities;
    private readonly FacilityTestLookup _tests;
    private readonly ReportsService _reports;
    private readonly SystemService _system;
    private readonly AutomationRunReader _runs;
    private readonly LogsService _logs;
    private readonly IApiHealthRunStore? _apiHealth;
    private readonly ILiveProcessUtilizationService? _pulse;
    private readonly IMemoryCache _cache;
    private readonly IOptions<LinkUiFeatureOptions> _features;
    private readonly ILogger<HomeOverviewService> _logger;

    public HomeOverviewService(
        IFacilityServiceClient facilities,
        FacilityTestLookup tests,
        ReportsService reports,
        SystemService system,
        AutomationRunReader runs,
        LogsService logs,
        IApiHealthRunStore? apiHealth,
        ILiveProcessUtilizationService? pulse,
        IMemoryCache cache,
        IOptions<LinkUiFeatureOptions> features,
        ILogger<HomeOverviewService> logger)
    {
        _facilities = facilities;
        _tests = tests;
        _reports = reports;
        _system = system;
        _runs = runs;
        _logs = logs;
        _apiHealth = apiHealth;
        _pulse = pulse;
        _cache = cache;
        _features = features;
        _logger = logger;
    }

    public static HomeOverviewService Create(IServiceProvider services) => new(
        services.GetRequiredService<IFacilityServiceClient>(),
        services.GetRequiredService<FacilityTestLookup>(),
        services.GetRequiredService<ReportsService>(),
        services.GetRequiredService<SystemService>(),
        services.GetRequiredService<AutomationRunReader>(),
        services.GetRequiredService<LogsService>(),
        services.GetService<IApiHealthRunStore>(),
        services.GetService<ILiveProcessUtilizationService>(),
        services.GetRequiredService<IMemoryCache>(),
        services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
        services.GetRequiredService<ILogger<HomeOverviewService>>());

    public async Task<HomeOverviewModel> LoadAsync(CancellationToken cancellationToken)
    {
        var automationOn = _features.Value.AutomationEnabled;
        var cacheKey = automationOn ? CacheKey : CacheKey + ":off";
        if (_cache.TryGetValue(cacheKey, out HomeOverviewModel? cached) && cached is not null)
            return cached;

        var facilitiesTask = LoadFacilitiesAsync(cancellationToken, automationOn);
        var reportsTask = LoadReportsAsync(cancellationToken, automationOn);
        var healthTask = LoadHealthAsync(cancellationToken, automationOn);
        var logsTask = LoadLogsAsync(cancellationToken);
        var activityTask = LoadActivityAsync(cancellationToken);
        var pulseTask = automationOn
            ? LoadPulseAsync(cancellationToken)
            : Task.FromResult(new ServicePulseCard { Reachable = true });
        var runsTask = automationOn
            ? LoadRunsAsync(cancellationToken)
            : Task.FromResult(new RunCard { Reachable = true });
        await Task.WhenAll(facilitiesTask, reportsTask, healthTask, logsTask, activityTask, pulseTask, runsTask);

        var loadedAt = DateTimeOffset.UtcNow;
        var facilities = await facilitiesTask;
        var health = await healthTask;
        var logs = await logsTask;
        var activity = await activityTask;
        var pulse = await pulseTask;
        var ownershipKnown = !automationOn || facilities.Regular is int;
        var model = new HomeOverviewModel
        {
            Facilities = facilities,
            Reports = await reportsTask,
            Health = health,
            Runs = automationOn ? await runsTask : new RunCard { Reachable = true },
            Logs = logs,
            Activity = activity,
            Pulse = pulse,
            Issues = HomeOverviewRules.Issues(
                facilities.Reachable,
                ownershipKnown,
                automationOn,
                activity,
                health,
                logs,
                pulse,
                loadedAt),
            AutomationVisible = automationOn,
            RealScope = HomeOverviewRules.RealFacilityScope(automationOn, facilities.Regular),
            LoadedAt = loadedAt
        };
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Set(cacheKey, model, TimeSpan.FromSeconds(HomeOverviewRules.CacheSeconds));
        return model;
    }

    private Task<FacilityCard> LoadFacilitiesAsync(CancellationToken cancellationToken, bool classify) =>
        Guard("facilities", token => LoadFacilityCountsAsync(token, classify), failure => HomeOverviewRules.FacilitiesFromCounts(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Tenant service could not be reached."
                : "Tenant service call failed.",
            0,
            null,
            true,
            classify), cancellationToken);

    private async Task<FacilityCard> LoadFacilityCountsAsync(CancellationToken cancellationToken, bool classify)
    {
        var response = await _facilities.GetFacilityCountsAsync(new FacilityCountRequest(), cancellationToken);
        if (!TryCount(response, out var body, out var message))
            return HomeOverviewRules.FacilitiesFromCounts(false, message, 0, null, true, classify);

        // Test travels with the facility total. One counts call covers the split.
        return HomeOverviewRules.FacilitiesFromCounts(
            true,
            null,
            body.Total,
            classify ? body.Test : null,
            true,
            classify);
    }

    private static bool TryCount<T>(LinkApiResponse<T> response, out T body, out string? message)
        where T : class
    {
        if (response.IsSuccessStatusCode && response.Body is not null)
        {
            body = response.Body;
            message = null;
            return true;
        }

        body = null!;
        message = FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody);
        return false;
    }

    private Task<ReportCard> LoadReportsAsync(CancellationToken cancellationToken, bool stamp) =>
        Guard("reports", async token =>
        {
            var page = await _reports.LoadListAsync(new ReportsListQuery
            {
                Page = 1,
                PageSize = HomeOverviewRules.RowLimit,
                SortBy = "CreateDate",
                SortDir = "desc"
            }, token);
            if (page.LoadError is not null)
                return HomeOverviewRules.Reports(false, page.LoadError, 0, null);
            IReadOnlySet<string>? testIds = null;
            if (stamp && page.Reports.Count > 0)
            {
                var flags = await _tests.ForIdsAsync(page.Reports.Select(row => row.FacilityId), token);
                if (flags.Reachable)
                    testIds = new HashSet<string>(flags.Ids, StringComparer.OrdinalIgnoreCase);
            }

            return HomeOverviewRules.Reports(true, null, page.Paging.TotalCount, page.Reports, testIds);
        }, failure => HomeOverviewRules.Reports(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Report service could not be reached."
                : "Report service call failed.",
            0,
            null), cancellationToken);

    private async Task<HealthCard> LoadHealthAsync(CancellationToken cancellationToken, bool includeApiHealth)
    {
        var healthTask = Guard("health", async token =>
        {
            var page = await _system.LoadHealthAsync(null, includeServiceInfo: false, token);
            if (page.HealthError is not null)
                return (false, page.HealthError, (IReadOnlyList<(string, string)>)[]);
            var rows = page.Reports.Select(row => (row.Service, row.Status)).ToList();
            return (true, (string?)null, (IReadOnlyList<(string, string)>)rows);
        }, failure => (
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Admin.BFF could not be reached."
                : "Admin.BFF call failed.",
            (IReadOnlyList<(string, string)>)[]), cancellationToken);

        if (!includeApiHealth)
        {
            var skipped = await healthTask;
            return HomeOverviewRules.Health(skipped.Item1, skipped.Item2, skipped.Item3, true, null, null, null, null, null);
        }

        var apiTask = Guard("api-health", LoadApiHealthAsync, failure => (
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "API health storage could not be read."
                : "API health storage could not be read.",
            (Guid?)null,
            (string?)null,
            (string?)null,
            (string?)null), cancellationToken);

        await Task.WhenAll(healthTask, apiTask);
        var health = await healthTask;
        var api = await apiTask;
        return HomeOverviewRules.Health(health.Item1, health.Item2, health.Item3, api.Item1, api.Item2, api.Item3, api.Item4, api.Item5, api.Item6);
    }

    private async Task<(bool Reachable, string? Message, Guid? RunId, string? Mode, string? Service, string? When)> LoadApiHealthAsync(
        CancellationToken cancellationToken)
    {
        if (_apiHealth is null)
            return (false, "API health storage is not configured.", null, null, null, null);

        var contextTask = _apiHealth.GetLatestRunContextAsync(cancellationToken);
        var executionTask = _apiHealth.GetLatestExecutionRunStatusAsync(cancellationToken);
        await Task.WhenAll(contextTask, executionTask);

        AutomationRunRow? scenario = null;
        try
        {
            scenario = await _runs.FindLatestNamedRunAsync(HomeOverviewRules.ApiHealthScenarioName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The latest API health scenario run could not be read.");
        }

        var chosen = HomeOverviewRules.ChooseLatestApiHealth(
            await contextTask,
            await executionTask,
            scenario);
        if (chosen is null)
            return (true, null, null, null, null, null);

        return (true, null, chosen.RunId, chosen.Mode, chosen.Service, HomeOverviewRules.When(chosen.At));
    }

    private Task<RunCard> LoadRunsAsync(CancellationToken cancellationToken) =>
        Guard("runs", async token =>
        {
            var slice = await _runs.LoadHomeSliceAsync(HomeOverviewRules.RowLimit, token);
            return HomeOverviewRules.Runs(slice.Reachable, slice.Message, slice.ActiveCount, slice.Active, slice.Recent);
        }, failure => HomeOverviewRules.Runs(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? AutomationRunReader.UnreachableMessage
                : AutomationRunReader.UnreachableMessage,
            0,
            null,
            null), cancellationToken);

    private async Task<LogCard> LoadLogsAsync(CancellationToken cancellationToken)
    {
        var acquisitionTask = Guard(
            "acquisition-logs",
            token => _logs.LoadAcquisitionCountsAsync(HomeOverviewRules.TrendLength, token),
            _ => (AcquisitionCountLoad?)null,
            cancellationToken);
        var auditTask = Guard(
            "audit",
            token => _logs.LoadAuditErrorsAsync(AggregateCountLimits.DefaultAuditHours, token),
            _ => (AuditErrorLoad?)null,
            cancellationToken);

        await Task.WhenAll(acquisitionTask, auditTask);
        var acquisition = await acquisitionTask;
        var audit = await auditTask;
        var acquisitionOk = acquisition is { Ok: true };
        var auditOk = audit is { Ok: true };
        return HomeOverviewRules.Logs(
            acquisitionOk,
            acquisition?.Error ?? "Data acquisition could not be reached.",
            acquisition?.FailedTotal ?? 0,
            null,
            auditOk,
            audit?.Error,
            acquisitionOk,
            acquisition?.Days,
            auditOk,
            audit?.Errors ?? 0);
    }

    private Task<ActivityCard> LoadActivityAsync(CancellationToken cancellationToken) =>
        Guard("activity", async token =>
        {
            var load = await _reports.LoadActivityCountsAsync(HomeOverviewRules.TrendLength, token);
            if (!load.Ok || load.Counts is null)
                return new ActivityCard();

            var counts = load.Counts;
            DateTimeOffset? oldest = counts.OldestInFlightUtc is DateTime value
                ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
                : null;
            var trend = counts.CreatedPerDay.Select(day => new TrendDay
            {
                Day = day.Day,
                Reachable = true,
                Count = day.Count
            }).ToList();
            return HomeOverviewRules.Activity(
                true,
                counts.InFlight,
                oldest,
                true,
                counts.Submitted,
                true,
                counts.NotSubmitted,
                trend);
        }, _ => new ActivityCard(), cancellationToken);

    private Task<ServicePulseCard> LoadPulseAsync(CancellationToken cancellationToken) =>
        Guard("pulse", async token =>
        {
            if (_pulse is null)
                return HomeOverviewRules.Pulse(false, "Service metrics are not configured.", null);

            var live = await _pulse.GetAsync(token);
            var samples = live.Services.Select(item =>
                new PulseSample(item.Name, item.Group, item.CpuPercent, item.ApiP95Ms));
            return HomeOverviewRules.Pulse(
                live.Reachable,
                live.Reachable ? null : "Service metrics could not be read.",
                samples);
        }, failure => HomeOverviewRules.Pulse(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Service metrics could not be read."
                : "Service metrics could not be read.",
            null), cancellationToken);

    private Task<T> Guard<T>(
        string card,
        Func<CancellationToken, Task<T>> load,
        Func<HomeOverviewRules.CardFailure, T> failed,
        CancellationToken cancellationToken) =>
        HomeOverviewRules.LoadCardAsync(
            load,
            failed,
            cancellationToken,
            HomeOverviewRules.CardBudget,
            ex => _logger.LogWarning(
                "Home {Card} card failed ({ExceptionType}).",
                card,
                ex.GetType().Name));
}
