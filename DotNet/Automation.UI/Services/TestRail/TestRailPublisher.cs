using System.Text;
using Automation.UI.Models;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.Persistence;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services.TestRail;

public sealed class TestRailPublisher(
    IOptions<TestRailOptions> options,
    ITestRailApiClient api,
    IScenarioStore scenarioStore,
    IApiHealthRunStore apiHealthRunStore,
    ApiEndpointRegistry endpointRegistry,
    ILogger<TestRailPublisher> logger) : ITestRailPublisher
{
    private const int MaxCommentChars = 20_000;
    internal const string SharedScenarioRunName = "Link Automation UI - Scenario suite";
    internal const string SharedApiHealthRunName = "Link Automation UI - API Health suite";
    private readonly SemaphoreSlim _sharedRunGate = new(1, 1);
    // Suite id alone is not enough: scenario and API Health may share a suite and still
    // publish to differently named runs.
    private readonly Dictionary<(int SuiteId, string RunName), SharedSuiteRun> _sharedRuns = new();
    private readonly TestRailTargetResolver _targets = new(api, logger);

    public async Task PublishScenarioRunAsync(
        ScenarioTestRailPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opts = options.Value;
            if (!opts.IsConfigured)
            {
                logger.LogDebug("TestRail publisher skipped for scenario run {RunId}: not configured.", request.RunId);
                return;
            }

            if (request.Status == AutomationRunStatus.Cancelled)
            {
                logger.LogInformation(
                    "TestRail publisher skipped for scenario run {RunId}: cancelled runs are not posted.",
                    request.RunId);
                return;
            }

            if (!opts.HasScenarioSuite)
            {
                logger.LogWarning(
                    "TestRail publisher skipped for scenario run {RunId}: ScenarioSuiteId is not configured.",
                    request.RunId);
                return;
            }

            var scenario = request.ScenarioId is Guid scenarioId
                ? await scenarioStore.GetByIdAsync(scenarioId, cancellationToken)
                : null;

            var caseId = TestRailCaseMapper.ResolveScenarioCaseId(
                scenario?.TestRailCaseId,
                scenario?.Id ?? request.ScenarioId,
                scenario?.Name,
                opts.ScenarioCaseIds);

            if (caseId is null)
            {
                logger.LogWarning(
                    "TestRail publisher skipped for scenario run {RunId}: no TestRail case id mapped.",
                    request.RunId);
                return;
            }

            var statusId = TestRailStatusMapper.FromScenarioStatus(request.Status);
            var comment = BuildScenarioComment(request);
            var elapsed = FormatElapsed(request.StartedAt, request.FinishedAt);
            var result = new TestRailCaseResult
            {
                CaseId = caseId.Value,
                StatusId = statusId,
                Comment = comment,
                Elapsed = elapsed,
                Attachment = ShouldAttach(statusId) ? EncodeAttachment(request.Logs, request.Error) : null,
                AttachmentFileName = ShouldAttach(statusId) ? $"scenario-run-{request.RunId:D}.log" : null
            };

            var runName = opts.UseSharedRun
                ? SharedScenarioRunName
                : $"Link Automation UI - {request.RunName} - {request.RunId:D}";

            await PublishResultsAsync(
                opts,
                opts.ScenarioSuiteId,
                opts.ScenarioSuiteName,
                "ScenarioSuiteId",
                opts.SharedScenarioRunId,
                runName,
                [result],
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail publish failed for scenario run {RunId}. Automation run is unaffected.", request.RunId);
        }
    }

    public async Task PublishApiHealthRunAsync(
        ApiHealthTestRailPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opts = options.Value;
            if (!opts.IsConfigured)
            {
                logger.LogDebug("TestRail publisher skipped for API Health run {RunId}: not configured.", request.RunId);
                return;
            }

            if (request.Cancelled)
            {
                logger.LogInformation(
                    "TestRail publisher skipped for API Health run {RunId}: cancelled runs are not posted.",
                    request.RunId);
                return;
            }

            if (!opts.HasApiHealthSuite)
            {
                logger.LogWarning(
                    "TestRail publisher skipped for API Health run {RunId}: ApiHealthSuiteId is not configured.",
                    request.RunId);
                return;
            }

            var definitions = string.Equals(request.Scope, "Service", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(request.ServiceName)
                    ? endpointRegistry.GetByService(request.ServiceName)
                    : endpointRegistry.GetAll();

            var keys = definitions.Select(d => d.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var resultsByKey = keys.Count == 0
                ? new Dictionary<string, ApiTestRunResult>()
                : await apiHealthRunStore.GetLatestResultsForRunAsync(request.RunId, keys, cancellationToken);

            var defByKey = definitions.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
            var caseResults = new List<TestRailCaseResult>();

            foreach (var (key, result) in resultsByKey)
            {
                defByKey.TryGetValue(key, out var definition);
                var caseId = TestRailCaseMapper.ResolveApiHealthCaseId(
                    definition?.TestRailCaseId,
                    result.EndpointKey,
                    result.EndpointName,
                    opts.ApiHealthCaseIds);

                if (caseId is null)
                    continue;

                var statusId = TestRailStatusMapper.FromApiHealthResult(result.Passed, result.Skipped);
                if (statusId is null)
                    continue;

                var comment = BuildApiHealthComment(result, request.Error);
                var attachment = ShouldAttach(statusId.Value)
                    ? Encoding.UTF8.GetBytes(comment ?? result.ErrorMessage ?? "Failed")
                    : null;

                caseResults.Add(new TestRailCaseResult
                {
                    CaseId = caseId.Value,
                    StatusId = statusId.Value,
                    Comment = comment,
                    Elapsed = result.DurationMs > 0 ? FormatElapsedMs(result.DurationMs) : null,
                    Attachment = attachment,
                    AttachmentFileName = attachment is null ? null : $"api-health-{SanitizeFileName(result.EndpointKey)}.txt"
                });
            }

            caseResults = CollapseApiHealthResults(caseResults);

            if (caseResults.Count == 0)
            {
                logger.LogInformation(
                    "TestRail publisher skipped for API Health run {RunId}: no mapped TestRail case ids.",
                    request.RunId);
                return;
            }

            var runName = opts.UseSharedRun
                ? SharedApiHealthRunName
                : $"Link Automation UI - API Health {request.Scope} {request.ServiceName} - {request.RunId:D}".Trim();

            await PublishResultsAsync(
                opts,
                opts.ApiHealthSuiteId,
                opts.ApiHealthSuiteName,
                "ApiHealthSuiteId",
                opts.SharedApiHealthRunId,
                runName,
                caseResults,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail publish failed for API Health run {RunId}. Automation run is unaffected.", request.RunId);
        }
    }

    /// <summary>
    /// A TestRail run has one test per case. Endpoints that share a case id post one result.
    /// When their statuses differ, that result is Failed and the comment names each endpoint.
    /// </summary>
    private List<TestRailCaseResult> CollapseApiHealthResults(List<TestRailCaseResult> results)
    {
        var groups = new List<List<TestRailCaseResult>>();
        var indexByCase = new Dictionary<int, int>();
        foreach (var result in results)
        {
            if (!indexByCase.TryGetValue(result.CaseId, out var index))
            {
                index = groups.Count;
                indexByCase[result.CaseId] = index;
                groups.Add([]);
            }

            groups[index].Add(result);
        }

        var collapsed = new List<TestRailCaseResult>(groups.Count);
        foreach (var group in groups)
        {
            if (group.Count == 1)
            {
                collapsed.Add(group[0]);
                continue;
            }

            var sameStatus = group.All(result => result.StatusId == group[0].StatusId);
            var statusId = sameStatus ? group[0].StatusId : TestRailStatusMapper.Failed;
            var comment = BuildCollapsedComment(group, sameStatus);
            byte[]? attachment = null;
            string? attachmentFileName = null;
            if (ShouldAttach(statusId))
            {
                attachment = Encoding.UTF8.GetBytes(comment ?? "Failed");
                attachmentFileName = $"api-health-c{group[0].CaseId}.txt";
            }

            logger.LogInformation(
                "TestRail case C{CaseId} is mapped from {EndpointCount} API Health endpoints. Posting one {Status} result.",
                group[0].CaseId,
                group.Count,
                StatusLabel(statusId));

            collapsed.Add(new TestRailCaseResult
            {
                CaseId = group[0].CaseId,
                StatusId = statusId,
                Comment = comment,
                Attachment = attachment,
                AttachmentFileName = attachmentFileName
            });
        }

        return collapsed;
    }

    private static string BuildCollapsedComment(List<TestRailCaseResult> group, bool sameStatus)
    {
        var sb = new StringBuilder();
        if (!sameStatus)
        {
            sb.AppendLine("These endpoints mapped to the same TestRail case and did not all pass. Posted as Failed.");
            foreach (var result in group)
                sb.AppendLine($"- {FirstLine(result.Comment)}: {StatusLabel(result.StatusId)}");
            sb.AppendLine();
        }

        foreach (var result in group)
        {
            if (!string.IsNullOrWhiteSpace(result.Comment))
            {
                sb.AppendLine(result.Comment.Trim());
                sb.AppendLine();
            }
        }

        return Truncate(sb.ToString());
    }

    private static string FirstLine(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return "endpoint";

        var line = comment.Split('\n', 2)[0].Trim();
        return line.Length == 0 ? "endpoint" : line;
    }

    private static string StatusLabel(int statusId) => statusId switch
    {
        TestRailStatusMapper.Passed => "Passed",
        TestRailStatusMapper.Blocked => "Blocked",
        TestRailStatusMapper.Failed => "Failed",
        _ => statusId.ToString()
    };

    private async Task PublishResultsAsync(
        TestRailOptions opts,
        int configuredSuiteId,
        string configuredSuiteName,
        string suiteSettingName,
        int? configuredSharedRunId,
        string runName,
        IReadOnlyList<TestRailCaseResult> results,
        CancellationToken cancellationToken)
    {
        var caseIds = results.Select(r => r.CaseId).Distinct().ToList();
        int? runId;
        if (opts.UseSharedRun && configuredSharedRunId is > 0)
        {
            // A pinned run created with include_all false rejects results for cases it does not
            // contain. Read the run and add any missing cases before posting.
            runId = await EnsurePinnedRunAsync(configuredSharedRunId.Value, caseIds, cancellationToken);
        }
        else
        {
            var projectId = await _targets.ResolveProjectIdAsync(opts, cancellationToken);
            if (projectId is null)
                return;

            var suiteId = await _targets.ResolveSuiteIdAsync(
                projectId.Value,
                configuredSuiteId,
                configuredSuiteName,
                suiteSettingName,
                cancellationToken);
            if (suiteId is null)
                return;

            runId = await GetOrCreateRunAsync(opts, projectId.Value, suiteId.Value, runName, caseIds, cancellationToken);
        }
        if (runId is null)
            return;

        IReadOnlyList<TestRailResultDto> posted;
        try
        {
            posted = await api.AddResultsForCasesAsync(runId.Value, results, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail add_results_for_cases failed for TestRail run {TestRailRunId}.", runId);
            return;
        }

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (result.Attachment is null || result.Attachment.Length == 0)
                continue;

            var postedResult = FindPostedResult(posted, results.Count, index, result.CaseId);
            if (postedResult is null)
            {
                logger.LogWarning(
                    "TestRail attachment skipped for case C{CaseId} in TestRail run {TestRailRunId}: no matching result in the add_results_for_cases response ({ResultCount} results returned).",
                    result.CaseId,
                    runId,
                    posted.Count);
                continue;
            }

            try
            {
                await api.AddAttachmentToResultAsync(
                    postedResult.Id,
                    result.AttachmentFileName ?? "failure.log",
                    result.Attachment,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "TestRail add_attachment_to_result failed for result {ResultId}.", postedResult.Id);
            }
        }
    }

    private async Task<int?> EnsurePinnedRunAsync(
        int runId,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken)
    {
        // Same in-process gate as run creation. Two overlapping updates would each replace case_ids.
        await _sharedRunGate.WaitAsync(cancellationToken);
        try
        {
            var ensured = await EnsureCasesOnRunAsync(runId, caseIds, cancellationToken);
            return ensured?.RunId;
        }
        finally
        {
            _sharedRunGate.Release();
        }
    }

    private async Task<int?> GetOrCreateRunAsync(
        TestRailOptions opts,
        int projectId,
        int suiteId,
        string runName,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken)
    {
        if (!opts.UseSharedRun)
            return await CreateRunAsync(projectId, suiteId, runName, caseIds, cancellationToken);

        // One creator per process. Two publishes finishing together used to each call add_run.
        await _sharedRunGate.WaitAsync(cancellationToken);
        try
        {
            var cacheKey = (suiteId, runName);
            if (_sharedRuns.TryGetValue(cacheKey, out var shared))
            {
                // Re-read the run from TestRail rather than trusting the cache. update_run replaces
                // case_ids, so cases added by another instance or by hand must be kept, and a run that
                // was closed since it was cached must not be reused.
                var refreshed = await EnsureCasesOnRunAsync(shared.RunId, caseIds, cancellationToken);
                if (refreshed is not null)
                {
                    _sharedRuns[cacheKey] = refreshed;
                    return refreshed.RunId;
                }

                _sharedRuns.Remove(cacheKey);
            }

            IReadOnlyList<TestRailRunDetails> openRuns;
            try
            {
                openRuns = await api.GetOpenRunsAsync(projectId, suiteId, cancellationToken) ?? [];
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TestRail get_runs failed for suite {SuiteId}.", suiteId);
                return null;
            }

            // First match in get_runs order. TestRail lists newer runs first.
            var match = openRuns.FirstOrDefault(run => IsReusableSharedRun(run, suiteId, runName));
            if (match is not null)
            {
                var adopted = await EnsureCasesOnRunAsync(match.Id, caseIds, cancellationToken);
                if (adopted is null)
                    return null;

                _sharedRuns[cacheKey] = adopted;
                logger.LogInformation(
                    "Reusing open TestRail run {TestRailRunId} named {RunName}.",
                    adopted.RunId,
                    runName);
                return adopted.RunId;
            }

            var created = await CreateRunAsync(projectId, suiteId, runName, caseIds, cancellationToken);
            if (created is int runId)
                _sharedRuns[cacheKey] = new SharedSuiteRun(runId, includeAll: false, caseIds);
            return created;
        }
        finally
        {
            _sharedRunGate.Release();
        }
    }

    /// <summary>
    /// Makes sure <paramref name="caseIds"/> are in the run. An include_all run already contains
    /// every case, so it is left unchanged. A selective run is updated with the union of its
    /// current cases and the missing ones. update_run replaces case_ids. A completed run is
    /// rejected, because TestRail does not accept results on it.
    /// </summary>
    private async Task<SharedSuiteRun?> EnsureCasesOnRunAsync(
        int runId,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken)
    {
        TestRailRunDetails? run;
        try
        {
            run = await api.GetRunAsync(runId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail get_run failed for run {TestRailRunId}.", runId);
            return null;
        }

        if (run is null || run.Id <= 0)
        {
            logger.LogError("TestRail get_run returned no run for {TestRailRunId}.", runId);
            return null;
        }

        if (run.IsCompleted)
        {
            // TestRail does not accept results or case changes on a closed run.
            logger.LogWarning("TestRail run {TestRailRunId} is completed. Results were not posted.", run.Id);
            return null;
        }

        if (run.IncludeAll)
            return new SharedSuiteRun(run.Id, includeAll: true, caseIds);

        IReadOnlyList<int>? existing;
        try
        {
            existing = await api.GetRunCaseIdsAsync(run.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail get_tests failed for run {TestRailRunId}.", run.Id);
            return null;
        }

        if (existing is null)
        {
            logger.LogError("TestRail get_tests returned no case list for run {TestRailRunId}.", run.Id);
            return null;
        }

        var have = existing.ToHashSet();
        var missing = caseIds.Where(id => !have.Contains(id)).ToList();
        var union = have.Concat(missing).Distinct().OrderBy(id => id).ToList();
        if (missing.Count > 0)
        {
            try
            {
                await api.UpdateRunCaseIdsAsync(run.Id, union, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TestRail update_run failed for run {TestRailRunId}.", run.Id);
                return null;
            }
        }

        return new SharedSuiteRun(run.Id, includeAll: false, union);
    }

    private static bool IsReusableSharedRun(TestRailRunDetails run, int suiteId, string runName)
    {
        if (run.Id <= 0 || run.IsCompleted)
            return false;
        if (run.SuiteId > 0 && run.SuiteId != suiteId)
            return false;
        return string.Equals(run.Name.Trim(), runName.Trim(), StringComparison.Ordinal);
    }

    private async Task<int?> CreateRunAsync(
        int projectId,
        int suiteId,
        string runName,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken)
    {
        try
        {
            return await api.AddRunAsync(projectId, suiteId, runName, caseIds, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail add_run failed for suite {SuiteId}.", suiteId);
            return null;
        }
    }

    private sealed class SharedSuiteRun(int runId, bool includeAll, IReadOnlyList<int> caseIds)
    {
        public int RunId { get; } = runId;
        public bool IncludeAll { get; } = includeAll;
        public HashSet<int> CaseIds { get; } = [.. caseIds];
    }

    /// <summary>
    /// Finds the TestRail result created for a posted case. Matches on case id when the response
    /// carries one, otherwise on position (TestRail returns results in the order they were posted).
    /// </summary>
    internal static TestRailResultDto? FindPostedResult(
        IReadOnlyList<TestRailResultDto> posted,
        int postedCount,
        int index,
        int caseId)
    {
        var byCase = posted.FirstOrDefault(p => p.CaseId == caseId && p.Id > 0);
        if (byCase is not null)
            return byCase;

        if (posted.Count == postedCount && index < posted.Count)
        {
            var byPosition = posted[index];
            if (byPosition.Id > 0 && (byPosition.CaseId <= 0 || byPosition.CaseId == caseId))
                return byPosition;
        }

        return null;
    }

    private static bool ShouldAttach(int statusId) =>
        statusId == TestRailStatusMapper.Failed || statusId == TestRailStatusMapper.Blocked;

    private static string? BuildScenarioComment(ScenarioTestRailPublishRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Automation.UI scenario run {request.RunId:D}");
        sb.AppendLine($"Status: {request.Status}");
        if (!string.IsNullOrWhiteSpace(request.RunName))
            sb.AppendLine($"Run: {request.RunName}");
        if (!string.IsNullOrWhiteSpace(request.Error))
            sb.AppendLine($"Error: {request.Error}");

        if (request.Logs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Logs:");
            foreach (var line in request.Logs.TakeLast(200))
                sb.AppendLine(line);
        }

        return Truncate(sb.ToString());
    }

    private static string? BuildApiHealthComment(ApiTestRunResult result, string? runError)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{result.ServiceName} :: {result.EndpointName}");
        if (result.Skipped)
            sb.AppendLine($"Skipped: {result.SkipReason}");
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            sb.AppendLine($"Error: {result.ErrorMessage}");
        if (!string.IsNullOrWhiteSpace(runError))
            sb.AppendLine($"Run error: {runError}");
        if (result.ActualStatusCode.HasValue)
            sb.AppendLine($"HTTP {result.RequestMethod} {result.RequestUrl} → {result.ActualStatusCode} (expected {result.ExpectedStatusCode})");
        if (!string.IsNullOrWhiteSpace(result.TraceId))
            sb.AppendLine($"TraceId: {result.TraceId}");
        if (!string.IsNullOrWhiteSpace(result.ResponseSnippet))
        {
            sb.AppendLine("Response:");
            sb.AppendLine(result.ResponseSnippet);
        }
        else if (!string.IsNullOrWhiteSpace(result.ResponseBody))
        {
            sb.AppendLine("Response:");
            sb.AppendLine(result.ResponseBody);
        }

        return Truncate(sb.ToString());
    }

    private static byte[]? EncodeAttachment(IReadOnlyList<string> logs, string? error)
    {
        if (logs.Count == 0 && string.IsNullOrWhiteSpace(error))
            return null;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(error))
        {
            sb.AppendLine("Error:");
            sb.AppendLine(error);
            sb.AppendLine();
        }

        foreach (var line in logs)
            sb.AppendLine(line);

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string? FormatElapsed(DateTimeOffset? startedAt, DateTimeOffset? finishedAt)
    {
        if (startedAt is null || finishedAt is null)
            return null;

        var ms = Math.Max(0, (finishedAt.Value - startedAt.Value).TotalMilliseconds);
        return FormatElapsedMs(ms);
    }

    internal static string FormatElapsedMs(double milliseconds)
    {
        var seconds = Math.Max(1, (int)Math.Round(milliseconds / 1000.0));
        if (seconds < 60)
            return $"{seconds}s";

        var minutes = seconds / 60;
        var remainder = seconds % 60;
        return remainder == 0 ? $"{minutes}m" : $"{minutes}m {remainder}s";
    }

    private static string Truncate(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxCommentChars)
            return trimmed;
        return trimmed[..MaxCommentChars];
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "result" : sanitized;
    }
}
