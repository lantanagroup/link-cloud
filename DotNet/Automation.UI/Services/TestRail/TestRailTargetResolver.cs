using System.Collections.Concurrent;

namespace Automation.UI.Services.TestRail;

/// <summary>
/// Turns the configured project and suite (a number or a name) into TestRail ids.
/// Names are looked up once and cached for the life of the process; failed lookups are not cached.
/// </summary>
public sealed class TestRailTargetResolver(ITestRailApiClient api, ILogger logger)
{
    private readonly ConcurrentDictionary<string, int> _projectIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _suiteIds = new(StringComparer.OrdinalIgnoreCase);

    public async Task<int?> ResolveProjectIdAsync(TestRailOptions options, CancellationToken cancellationToken = default)
    {
        if (options.ProjectId > 0)
            return options.ProjectId;

        var name = options.ProjectName.Trim();
        if (name.Length == 0)
            return null;

        if (_projectIds.TryGetValue(name, out var cached))
            return cached;

        var projects = await api.GetProjectsAsync(cancellationToken);
        var match = FindByName(projects, name, out var problem);
        if (match is null)
        {
            logger.LogError(
                "TestRail project {ProjectName} from {Setting} {Problem}. Results are not published.",
                name,
                $"{TestRailOptions.SectionName}:ProjectId",
                problem);
            return null;
        }

        _projectIds[name] = match.Id;
        logger.LogInformation("TestRail project {ProjectName} is project id {ProjectId}.", name, match.Id);
        return match.Id;
    }

    public async Task<int?> ResolveSuiteIdAsync(
        int projectId,
        int suiteId,
        string suiteName,
        string settingName,
        CancellationToken cancellationToken = default)
    {
        if (suiteId > 0)
            return suiteId;

        var name = suiteName.Trim();
        if (name.Length == 0)
            return null;

        var cacheKey = $"{projectId}|{name}";
        if (_suiteIds.TryGetValue(cacheKey, out var cached))
            return cached;

        var suites = await api.GetSuitesAsync(projectId, cancellationToken);
        var match = FindByName(suites, name, out var problem);
        if (match is null)
        {
            logger.LogError(
                "TestRail suite {SuiteName} from {Setting} {Problem} in project {ProjectId}. Results are not published.",
                name,
                $"{TestRailOptions.SectionName}:{settingName}",
                problem,
                projectId);
            return null;
        }

        _suiteIds[cacheKey] = match.Id;
        logger.LogInformation(
            "TestRail suite {SuiteName} is suite id {SuiteId} in project {ProjectId}.",
            name,
            match.Id,
            projectId);
        return match.Id;
    }

    /// <summary>
    /// Finds one item by exact name, ignoring case. When several share the name, an active one
    /// wins over completed ones; two or more active matches are reported as ambiguous.
    /// </summary>
    internal static TestRailNamedItem? FindByName(
        IReadOnlyList<TestRailNamedItem> items,
        string name,
        out string problem)
    {
        var matches = items
            .Where(i => string.Equals(i.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            problem = "was not found";
            return null;
        }

        var active = matches.Where(m => !m.IsCompleted).ToList();
        var candidates = active.Count > 0 ? active : matches;
        if (candidates.Count > 1)
        {
            problem = $"matches {candidates.Count} items; set the numeric id instead";
            return null;
        }

        problem = string.Empty;
        return candidates[0];
    }
}
