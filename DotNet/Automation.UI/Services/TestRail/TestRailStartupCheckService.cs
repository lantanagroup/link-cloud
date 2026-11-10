using Microsoft.Extensions.Options;

namespace Automation.UI.Services.TestRail;

/// <summary>
/// When publishing is configured, makes read-only TestRail calls once after startup to confirm the
/// site is reachable, the credentials work, and the project and suites exist. Results are logged
/// only; a failure never affects the service or its runs.
/// </summary>
public sealed class TestRailStartupCheckService(
    IOptions<TestRailOptions> options,
    ITestRailApiClient api,
    ILogger<TestRailStartupCheckService> logger) : BackgroundService
{
    internal static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(60);
    internal const int MaxSuitesListed = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await RunCheckAsync(stoppingToken);
    }

    internal async Task RunCheckAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.IsConfigured)
            return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(CheckTimeout);

        try
        {
            var resolver = new TestRailTargetResolver(api, logger);
            var projectId = await resolver.ResolveProjectIdAsync(opts, timeout.Token);
            if (projectId is null)
                return;

            var suites = await api.GetSuitesAsync(projectId.Value, timeout.Token);
            logger.LogInformation(
                "TestRail connection check passed for {BaseUrl}: project {ProjectId} has {SuiteCount} suites.",
                opts.BaseUrl,
                projectId.Value,
                suites.Count);

            CheckSuite(opts.ScenarioSuiteId, opts.ScenarioSuiteName, "ScenarioSuiteId", projectId.Value, suites);
            CheckSuite(opts.ApiHealthSuiteId, opts.ApiHealthSuiteName, "ApiHealthSuiteId", projectId.Value, suites);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "TestRail connection check failed for {BaseUrl}: {Error}. Runs are unaffected; publishing will retry on each run.",
                opts.BaseUrl,
                ex.Message);
        }
    }

    private void CheckSuite(
        int suiteId,
        string suiteName,
        string settingName,
        int projectId,
        IReadOnlyList<TestRailNamedItem> suites)
    {
        var setting = $"{TestRailOptions.SectionName}:{settingName}";
        if (suiteId <= 0 && string.IsNullOrWhiteSpace(suiteName))
        {
            logger.LogInformation(
                "{Setting} is not set. Suites in project {ProjectId}: {Suites}.",
                setting,
                projectId,
                ListSuites(suites));
            return;
        }

        var found = suiteId > 0
            ? suites.FirstOrDefault(s => s.Id == suiteId)
            : TestRailTargetResolver.FindByName(suites, suiteName.Trim(), out _);

        if (found is null)
        {
            logger.LogWarning(
                "{Setting} {Suite} is not a suite in project {ProjectId}. Suites in the project: {Suites}.",
                setting,
                TestRailStartupReport.Describe(suiteId, suiteName),
                projectId,
                ListSuites(suites));
            return;
        }

        logger.LogInformation("{Setting} is suite {SuiteId} ({SuiteName}).", setting, found.Id, found.Name);
    }

    internal static string ListSuites(IReadOnlyList<TestRailNamedItem> suites)
    {
        if (suites.Count == 0)
            return "none";

        var listed = suites
            .Where(s => !s.IsCompleted)
            .Take(MaxSuitesListed)
            .Select(s => $"S{s.Id} {s.Name}")
            .ToList();
        if (listed.Count == 0)
            return "none active";

        var more = suites.Count(s => !s.IsCompleted) - listed.Count;
        return more > 0 ? $"{string.Join("; ", listed)}; and {more} more" : string.Join("; ", listed);
    }
}
