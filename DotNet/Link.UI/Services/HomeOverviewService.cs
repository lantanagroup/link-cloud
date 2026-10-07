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
    private readonly AutomationOwnershipLookup _ownership;
    private readonly ReportsService _reports;
    private readonly SystemService _system;
    private readonly AutomationRunReader _runs;
    private readonly LogsService _logs;
    private readonly IApiHealthRunStore? _apiHealth;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomeOverviewService> _logger;

    public HomeOverviewService(
        IFacilityServiceClient facilities,
        AutomationOwnershipLookup ownership,
        ReportsService reports,
        SystemService system,
        AutomationRunReader runs,
        LogsService logs,
        IApiHealthRunStore? apiHealth,
        IMemoryCache cache,
        ILogger<HomeOverviewService> logger)
    {
        _facilities = facilities;
        _ownership = ownership;
        _reports = reports;
        _system = system;
        _runs = runs;
        _logs = logs;
        _apiHealth = apiHealth;
        _cache = cache;
        _logger = logger;
    }

    public static HomeOverviewService Create(IServiceProvider services) => new(
        services.GetRequiredService<IFacilityServiceClient>(),
        services.GetRequiredService<AutomationOwnershipLookup>(),
        services.GetRequiredService<ReportsService>(),
        services.GetRequiredService<SystemService>(),
        services.GetRequiredService<AutomationRunReader>(),
        services.GetRequiredService<LogsService>(),
        services.GetService<IApiHealthRunStore>(),
        services.GetRequiredService<IMemoryCache>(),
        services.GetRequiredService<ILogger<HomeOverviewService>>());

    public async Task<HomeOverviewModel> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out HomeOverviewModel? cached) && cached is not null)
            return cached;

        var facilities = LoadFacilitiesAsync(cancellationToken);
        var reports = LoadReportsAsync(cancellationToken);
        var health = LoadHealthAsync(cancellationToken);
        var runs = LoadRunsAsync(cancellationToken);
        var logs = LoadLogsAsync(cancellationToken);
        await Task.WhenAll(facilities, reports, health, runs, logs);

        var model = new HomeOverviewModel
        {
            Facilities = await facilities,
            Reports = await reports,
            Health = await health,
            Runs = await runs,
            Logs = await logs,
            LoadedAt = DateTimeOffset.UtcNow
        };
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Set(CacheKey, model, TimeSpan.FromSeconds(HomeOverviewRules.CacheSeconds));
        return model;
    }

    private Task<FacilityCard> LoadFacilitiesAsync(CancellationToken cancellationToken) =>
        Guard("facilities", async token =>
        {
            var listTask = _facilities.GetFacilityListAsync(cancellationToken: token);
            var ownershipTask = _ownership.GetSnapshotAsync(token);
            await Task.WhenAll(listTask, ownershipTask);
            var list = await listTask;
            var (index, ownershipReachable) = await ownershipTask;
            if (!HomeOverviewRules.TryReadFacilities(list.StatusCode, list.IsSuccessStatusCode, list.Body, out var ids))
            {
                var message = list.StatusCode == 0
                    ? "Tenant service could not be reached."
                    : $"Tenant service returned HTTP {list.StatusCode}.";
                return HomeOverviewRules.Facilities(false, message, ownershipReachable, null, null);
            }

            return HomeOverviewRules.Facilities(true, null, ownershipReachable, ids, index);
        }, failure => HomeOverviewRules.Facilities(
            false,
            failure == HomeOverviewRules.CardFailure.Timeout
                ? "Tenant service could not be reached."
                : "Tenant service call failed.",
            true,
            null,
            null), cancellationToken);

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
        var rows = acquisition is { LoadError: null }
            ? acquisition.Logs.Take(HomeOverviewRules.RowLimit).Select(row => new HomeLogLine
            {
                Id = row.Id,
                FacilityId = row.FacilityId,
                Status = row.Status,
                Badge = HomeOverviewRules.LogBadge(row.Status),
                When = row.Created
            })
            : null;
        return HomeOverviewRules.Logs(
            acquisition is { LoadError: null },
            acquisition?.LoadError ?? "Data acquisition could not be reached.",
            acquisition?.Paging.TotalCount ?? 0,
            rows,
            audit is { LoadError: null },
            audit?.LoadError);
    }

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
