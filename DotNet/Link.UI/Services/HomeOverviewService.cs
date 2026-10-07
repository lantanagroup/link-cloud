using System.Globalization;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Sdk.Clients;
using Link.UI.Models;
using Microsoft.Extensions.Caching.Memory;

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
    private readonly IFacilityClassification _classification;
    private readonly ReportsService _reports;
    private readonly SystemService _system;
    private readonly AutomationRunReader _runs;
    private readonly LogsService _logs;
    private readonly IApiHealthRunStore? _apiHealth;
    private readonly ILiveProcessUtilizationService? _pulse;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomeOverviewService> _logger;

    public HomeOverviewService(
        IFacilityServiceClient facilities,
        IFacilityClassification classification,
        ReportsService reports,
        SystemService system,
        AutomationRunReader runs,
        LogsService logs,
        IApiHealthRunStore? apiHealth,
        ILiveProcessUtilizationService? pulse,
        IMemoryCache cache,
        ILogger<HomeOverviewService> logger)
    {
        _facilities = facilities;
        _classification = classification;
        _reports = reports;
        _system = system;
        _runs = runs;
        _logs = logs;
        _apiHealth = apiHealth;
        _pulse = pulse;
        _cache = cache;
        _logger = logger;
    }

    public static HomeOverviewService Create(IServiceProvider services) => new(
        services.GetRequiredService<IFacilityServiceClient>(),
        services.GetRequiredService<IFacilityClassification>(),
        services.GetRequiredService<ReportsService>(),
        services.GetRequiredService<SystemService>(),
        services.GetRequiredService<AutomationRunReader>(),
        services.GetRequiredService<LogsService>(),
        services.GetService<IApiHealthRunStore>(),
        services.GetService<ILiveProcessUtilizationService>(),
        services.GetRequiredService<IMemoryCache>(),
        services.GetRequiredService<ILogger<HomeOverviewService>>());

    public async Task<HomeOverviewModel> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out HomeOverviewModel? cached) && cached is not null)
            return cached;

        var classificationTask = Guard(
            "classification",
            token => _classification.ReadAsync(token),
            _ => new FacilityClassification { AutomationVisible = true, OwnershipReachable = false },
            cancellationToken);
        var facilityListTask = LoadFacilityListAsync(cancellationToken);
        var reportsTask = LoadReportsAsync(cancellationToken);
        var healthTask = LoadHealthAsync(cancellationToken);
        var logsTask = LoadLogsAsync(cancellationToken);
        var activityTask = LoadActivityAsync(cancellationToken);
        var pulseTask = LoadPulseAsync(cancellationToken);
        var classification = await classificationTask;
        var runsTask = classification.AutomationVisible
            ? LoadRunsAsync(cancellationToken)
            : Task.FromResult(new RunCard { Reachable = true });
        await Task.WhenAll(facilityListTask, reportsTask, healthTask, logsTask, activityTask, pulseTask, runsTask);

        var loadedAt = DateTimeOffset.UtcNow;
        var facilityList = await facilityListTask;
        var facilities = HomeOverviewRules.ForAudience(
            facilityList.Reachable
                ? HomeOverviewRules.Facilities(true, null, classification.OwnershipReachable, facilityList.Ids, classification.Ownership)
                : HomeOverviewRules.Facilities(false, facilityList.Message, classification.OwnershipReachable, null, null),
            classification.AutomationVisible);
        var health = await healthTask;
        var logs = await logsTask;
        var activity = await activityTask;
        var pulse = await pulseTask;
        var model = new HomeOverviewModel
        {
            Facilities = facilities,
            Reports = await reportsTask,
            Health = health,
            Runs = classification.AutomationVisible ? await runsTask : new RunCard { Reachable = true },
            Logs = logs,
            Activity = activity,
            Pulse = pulse,
            Issues = HomeOverviewRules.Issues(
                facilities.Reachable,
                classification.OwnershipReachable,
                classification.AutomationVisible,
                activity,
                health,
                logs,
                pulse,
                loadedAt),
            AutomationVisible = classification.AutomationVisible,
            RealScope = classification.RealScope,
            LoadedAt = loadedAt
        };
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Set(CacheKey, model, TimeSpan.FromSeconds(HomeOverviewRules.CacheSeconds));
        return model;
    }

    private Task<FacilityListRead> LoadFacilityListAsync(CancellationToken cancellationToken) =>
        Guard("facilities", async token =>
        {
            var list = await _facilities.GetFacilityListAsync(cancellationToken: token);
            if (!HomeOverviewRules.TryReadFacilities(list.StatusCode, list.IsSuccessStatusCode, list.Body, out var ids))
            {
                var message = list.StatusCode == 0
                    ? "Tenant service could not be reached."
                    : $"Tenant service returned HTTP {list.StatusCode}.";
                return new FacilityListRead(false, message, []);
            }

            return new FacilityListRead(true, null, ids);
        }, failure => new FacilityListRead(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Tenant service could not be reached."
                : "Tenant service call failed.",
            []), cancellationToken);

    private sealed record FacilityListRead(bool Reachable, string? Message, IReadOnlyCollection<string> Ids);

    private Task<ReportCard> LoadReportsAsync(CancellationToken cancellationToken) =>
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
            return HomeOverviewRules.Reports(true, null, page.Paging.TotalCount, page.Reports);
        }, failure => HomeOverviewRules.Reports(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Report service could not be reached."
                : "Report service call failed.",
            0,
            null), cancellationToken);

    private async Task<HealthCard> LoadHealthAsync(CancellationToken cancellationToken)
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
        var (reachable, message, rows) = await healthTask;
        var (apiReachable, apiMessage, runId, mode, service, when) = await apiTask;
        return HomeOverviewRules.Health(reachable, message, rows, apiReachable, apiMessage, runId, mode, service, when);
    }

    private async Task<(bool Reachable, string? Message, Guid? RunId, string? Mode, string? Service, string? When)> LoadApiHealthAsync(
        CancellationToken cancellationToken)
    {
        if (_apiHealth is null)
            return (false, "API health storage is not configured.", null, null, null, null);

        var context = await _apiHealth.GetLatestRunContextAsync(cancellationToken);
        if (context is null || context.RunId == Guid.Empty)
            return (true, null, null, null, null, null);

        return (true, null, context.RunId, context.RunMode, context.ServiceName, HomeOverviewRules.When(context.StartedAt));
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
        var acquisitionTask = Guard("acquisition-logs", token => _logs.LoadAcquisitionAsync(new AcquisitionQuery
        {
            Status = HomeOverviewRules.ErrorLogStatuses.ToList(),
            Page = 1,
            PageSize = LogsRules.DefaultPageSize,
            SortBy = "ExecutionDate",
            SortDir = "desc"
        }, token), _ => (AcquisitionLogListPage?)null, cancellationToken);

        var auditTask = Guard("audit", token => _logs.LoadAuditAsync(new AuditQuery
        {
            Page = 1,
            PageSize = LogsRules.DefaultPageSize
        }, token), _ => (AuditListPage?)null, cancellationToken);

        await Task.WhenAll(acquisitionTask, auditTask);
        var acquisition = await acquisitionTask;
        var audit = await auditTask;
        return HomeOverviewRules.Logs(
            acquisition is { LoadError: null },
            acquisition?.LoadError ?? "Data acquisition could not be reached.",
            acquisition?.Paging.TotalCount ?? 0,
            null,
            audit is { LoadError: null },
            audit?.LoadError);
    }

    private Task<ActivityCard> LoadActivityAsync(CancellationToken cancellationToken) =>
        Guard("activity", async token =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var days = HomeOverviewRules.TrendDays(today);
            var inFlight = CountReportsAsync(HomeOverviewRules.InFlightStatuses, null, "asc", token);
            var submitted = CountReportsAsync([HomeOverviewRules.SubmittedStatus], null, "desc", token);
            var completed = CountReportsAsync([HomeOverviewRules.CompletedStatus], null, "desc", token);
            var trend = days
                .Select(day => CountReportsAsync(null, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "desc", token))
                .ToArray();
            await Task.WhenAll(new[] { inFlight, submitted, completed }.Concat(trend));

            var inFlightCount = await inFlight;
            var submittedCount = await submitted;
            var completedCount = await completed;
            var trendDays = new TrendDay[days.Count];
            for (var i = 0; i < days.Count; i++)
            {
                var dayCount = await trend[i];
                trendDays[i] = new TrendDay
                {
                    Day = days[i].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Reachable = dayCount.Ok,
                    Count = dayCount.Ok ? dayCount.Total : 0
                };
            }

            return HomeOverviewRules.Activity(
                inFlightCount.Ok,
                inFlightCount.Total,
                inFlightCount.Ok ? HomeOverviewRules.ParseIso(inFlightCount.FirstCreated) : null,
                submittedCount.Ok,
                submittedCount.Total,
                completedCount.Ok,
                completedCount.Total,
                trendDays);
        }, _ => new ActivityCard(), cancellationToken);

    private async Task<ReportCount> CountReportsAsync(
        IReadOnlyList<string>? statuses,
        string? created,
        string sortDir,
        CancellationToken cancellationToken)
    {
        var page = await _reports.LoadListAsync(new ReportsListQuery
        {
            Page = 1,
            PageSize = HomeOverviewRules.RowLimit,
            SortBy = "CreateDate",
            SortDir = sortDir,
            Status = statuses?.ToList(),
            Created = created
        }, cancellationToken);
        if (page.LoadError is not null)
            return new ReportCount(false, 0, null);
        var first = page.Reports.Count > 0 ? page.Reports[0].Created : null;
        return new ReportCount(true, page.Paging.TotalCount, first);
    }

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

    private sealed record ReportCount(bool Ok, long Total, string? FirstCreated);

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
