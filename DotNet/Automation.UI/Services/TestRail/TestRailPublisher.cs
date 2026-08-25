using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<int, int> _sharedRunIdsBySuite = new();

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

            if (opts.ScenarioSuiteId <= 0)
            {
                logger.LogWarning(
                    "TestRail publisher skipped for scenario run {RunId}: ScenarioSuiteId is not set (placeholder until LEGLINK-1062).",
                    request.RunId);
                return;
            }

            var scenario = request.ScenarioId is Guid scenarioId
                ? await scenarioStore.GetByIdAsync(scenarioId, cancellationToken)
                : null;

            var caseId = TestRailCaseMapper.ResolveScenarioCaseId(
                scenario?.TestRailCaseId,
                request.ScenarioId,
                scenario?.Name ?? request.RunName,
                opts.ScenarioCaseIds);

            if (caseId is null)
            {
                logger.LogInformation(
                    "TestRail publisher skipped for scenario run {RunId}: no TestRail case id mapped (set TestRailCaseId or Automation:TestRail:ScenarioCaseIds; real ids come from LEGLINK-1062).",
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
                AttachmentFileName = ShouldAttach(statusId) ? $"scenario-run-{request.RunId:N}.log" : null
            };

            var runName = opts.UseSharedRun
                ? "Link Automation UI - Scenario suite"
                : $"Link Automation UI - {request.RunName} - {request.RunId:N}";

            await PublishResultsAsync(
                opts,
                opts.ScenarioSuiteId,
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

            if (opts.ApiHealthSuiteId <= 0)
            {
                logger.LogWarning(
                    "TestRail publisher skipped for API Health run {RunId}: ApiHealthSuiteId is not set (placeholder until LEGLINK-1062).",
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

                var statusId = TestRailStatusMapper.FromApiHealthResult(result.Passed, result.Skipped, opts.SkipStatusId);
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

            if (caseResults.Count == 0)
            {
                logger.LogInformation(
                    "TestRail publisher skipped for API Health run {RunId}: no mapped TestRail case ids (set ApiEndpointDefinition.TestRailCaseId or Automation:TestRail:ApiHealthCaseIds; real ids come from LEGLINK-1062).",
                    request.RunId);
                return;
            }

            var runName = opts.UseSharedRun
                ? "Link Automation UI - API Health suite"
                : $"Link Automation UI - API Health {request.Scope} {request.ServiceName} - {request.RunId:N}".Trim();

            await PublishResultsAsync(
                opts,
                opts.ApiHealthSuiteId,
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

    private async Task PublishResultsAsync(
        TestRailOptions opts,
        int suiteId,
        int? configuredSharedRunId,
        string runName,
        IReadOnlyList<TestRailCaseResult> results,
        CancellationToken cancellationToken)
    {
        var caseIds = results.Select(r => r.CaseId).Distinct().ToList();
        var runId = await GetOrCreateRunAsync(opts, suiteId, configuredSharedRunId, runName, caseIds, cancellationToken);
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

        foreach (var result in results)
        {
            if (result.Attachment is null || result.Attachment.Length == 0)
                continue;

            var postedResult = posted.FirstOrDefault(p => p.CaseId == result.CaseId);
            if (postedResult is null || postedResult.Id <= 0)
                continue;

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

    private async Task<int?> GetOrCreateRunAsync(
        TestRailOptions opts,
        int suiteId,
        int? configuredSharedRunId,
        string runName,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken)
    {
        if (opts.UseSharedRun)
        {
            if (configuredSharedRunId is > 0)
                return configuredSharedRunId;

            if (_sharedRunIdsBySuite.TryGetValue(suiteId, out var existing))
                return existing;
        }

        try
        {
            var created = await api.AddRunAsync(opts.ProjectId, suiteId, runName, caseIds, cancellationToken);
            if (opts.UseSharedRun)
                _sharedRunIdsBySuite.TryAdd(suiteId, created);
            return created;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestRail add_run failed for suite {SuiteId}.", suiteId);
            return null;
        }
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
