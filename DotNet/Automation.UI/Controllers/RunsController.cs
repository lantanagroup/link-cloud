using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class RunsController(
    IAutomationRunManager runManager,
    ISnapshotStore snapshotStore,
    IScenarioStore scenarioStore,
    IQueryPlanTemplateStore queryPlanTemplateStore,
    INormalizationStore normalizationStore,
    IDataAcquisitionServiceClient dataAcqClient,
    IRunExportService runExportService,
    ILogger<RunsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = "createdAt",
        string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        // Normalize: accept "asc"/"desc" only, default to descending. Server-side
        // store-level whitelisting also clamps unknown sortBy values, so this is
        // belt-and-suspenders against URL tampering.
        var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        var stats = await runManager.GetDashboardStatsAsync(cancellationToken);
        var recentPage = await runManager.GetRunsPageAsync(pageNumber, pageSize, sortBy, descending, cancellationToken);
        var scenarios = (await scenarioStore.GetAllAsync(cancellationToken))
            .OrderBy(s => s.IsSystemScenario)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeRunMetas = await snapshotStore.GetActiveRunsAsync(cancellationToken);
        var activeRunSummaries = await Task.WhenAll(activeRunMetas.Select(meta => runManager.GetRunAsync(meta.RunId, cancellationToken)));

        var activeRunsSource = recentPage.PageNumber == 1
            ? recentPage.Runs
            : (await runManager.GetRunsPageAsync(1, pageSize, "createdAt", true, cancellationToken)).Runs;
        var statusActiveRuns = activeRunsSource
            .Where(r => r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running);

        var activeRuns = activeRunSummaries
            .Where(r => r != null && (r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running))
            .Select(r => r!)
            .Concat(statusActiveRuns)
            .GroupBy(r => r.RunId)
            .Select(g => g.First())
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        // Populate query plan templates for the shared scenario editor modal embedded in this view.
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(cancellationToken);
        ViewBag.NormalizationSuites = await normalizationStore.GetAllSuitesAsync(cancellationToken);

        var vm = new RunDashboardViewModel
        {
            Stats = stats,
            RecentRuns = recentPage.Runs,
            ActiveRuns = activeRuns,
            SavedScenarios = scenarios,
            // Echo paging/sort state to the view so headers + pager render
            // current state and click-to-toggle URLs are correct.
            PageNumber = recentPage.PageNumber,
            PageSize = recentPage.PageSize,
            TotalCount = recentPage.TotalCount,
            TotalPages = recentPage.TotalPages,
            SortBy = recentPage.SortBy,
            SortDescending = recentPage.SortDescending,
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> RecentRunsPartial(
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = "createdAt",
        string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        // Returns just the Recent Runs card markup so the dashboard can refresh
        // the table in place (sort / paginate / SignalR update) without a full
        // page navigation. The view model matches the partial's @model so the
        // partial is reused by both this action and the initial server render
        // in Index.cshtml — there's no divergence between the two templates.
        var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        var page = await runManager.GetRunsPageAsync(pageNumber, pageSize, sortBy, descending, cancellationToken);
        return PartialView("_RecentRunsTable", page);
    }

    [HttpGet]
    public async Task<IActionResult> DashboardStats(
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = "createdAt",
        string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        var stats = await runManager.GetDashboardStatsAsync(cancellationToken);
        var recentPage = await runManager.GetRunsPageAsync(pageNumber, pageSize, sortBy, descending, cancellationToken);

        var activeRunMetas = await snapshotStore.GetActiveRunsAsync(cancellationToken);
        var activeRunSummaries = await Task.WhenAll(activeRunMetas.Select(meta => runManager.GetRunAsync(meta.RunId, cancellationToken)));

        var activeRunsSource = recentPage.PageNumber == 1
            ? recentPage.Runs
            : (await runManager.GetRunsPageAsync(1, pageSize, "createdAt", true, cancellationToken)).Runs;
        var statusActiveRuns = activeRunsSource
            .Where(r => r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running);

        var activeRuns = activeRunSummaries
            .Where(r => r != null && (r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running))
            .Select(r => r!)
            .Concat(statusActiveRuns)
            .GroupBy(r => r.RunId)
            .Select(g => g.First())
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return Json(new
        {
            stats,
            recentRuns = recentPage.Runs,
            activeRuns,
            paging = new
            {
                pageNumber = recentPage.PageNumber,
                pageSize = recentPage.PageSize,
                totalCount = recentPage.TotalCount,
                totalPages = recentPage.TotalPages,
                sortBy = recentPage.SortBy,
                sortDir = recentPage.SortDir,
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartScenarioRequest request, CancellationToken cancellationToken)
    {
        if (request.Scenario == AutomationScenarioKind.Custom && request.ScenarioId is Guid scenarioId)
        {
            var scenario = await scenarioStore.GetByIdAsync(scenarioId, cancellationToken);
            if (scenario == null)
            {
                TempData["RunStartError"] = "Unable to start test: selected scenario was not found.";
                return RedirectToAction(nameof(Index));
            }

            await runManager.StartAsync(StartScenarioRequest.FromScenario(scenario), cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            TempData["RunStartError"] = errors.Count > 0
                ? $"Unable to start test: {string.Join(" | ", errors)}"
                : "Unable to start test due to invalid configuration.";
            return RedirectToAction(nameof(Index));
        }

        await runManager.StartAsync(request, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        return View(run);
    }

    [HttpGet]
    public async Task<IActionResult> Manifest(Guid id, CancellationToken cancellationToken)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        var manifest = await runManager.GetGenerationManifestAsync(id, cancellationToken);
        if (manifest == null)
            return RedirectToAction(nameof(Details), new { id });

        ViewBag.Run = run;
        ViewBag.RunId = id;
        return View("Manifest", manifest);
    }

    [HttpGet]
    public async Task<IActionResult> ManifestData(Guid id, CancellationToken cancellationToken)
    {
        var manifest = await runManager.GetGenerationManifestAsync(id, cancellationToken);
        if (manifest == null) return NoContent();
        return Json(manifest);
    }

    [HttpGet]
    public async Task<IActionResult> AbsUploadData(Guid id, CancellationToken cancellationToken)
    {
        var abs = await runManager.GetAbsUploadSnapshotAsync(id, cancellationToken);
        if (abs == null) return NoContent();
        return Json(abs);
    }

    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        return Json(run);
    }

    [HttpGet]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        // Export is only meaningful once the run has stopped collecting data;
        // exporting an in-flight run would race the polling loop and yield
        // half-populated domain snapshots.
        if (run.Status is not AutomationRunStatus.Succeeded
                       and not AutomationRunStatus.Failed
                       and not AutomationRunStatus.Cancelled)
        {
            return Conflict(new { error = "Run must be completed (Succeeded, Failed, or Cancelled) before it can be exported." });
        }

        var package = await runExportService.BuildAsync(id, cancellationToken);
        if (package == null)
            return NotFound();

        return File(package.Content, "application/zip", package.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await runManager.DeleteRunAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { pageNumber, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await runManager.CancelRunAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { pageNumber, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelJson([FromBody] RunActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Id == null || request.Id == Guid.Empty)
            return BadRequest(new { success = false, error = "Missing run ID" });

        var cancelled = await runManager.CancelRunAsync(request.Id, cancellationToken);
        return Ok(new { success = cancelled });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteJson([FromBody] RunActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Id == null || request.Id == Guid.Empty)
            return BadRequest(new { success = false, error = "Missing run ID" });

        var deleted = await runManager.DeleteRunAsync(request.Id, cancellationToken);
        return Ok(new { success = deleted });
    }

    public class RunActionRequest
    {
        public Guid Id { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> PipelineSnapshot(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await runManager.GetPipelineSnapshotAsync(id, cancellationToken);
        if (snapshot == null)
            return NoContent();

        return Json(snapshot);
    }

    /// <summary>
    /// Proxy endpoint for DA log drill-down. Returns a paged list of DA logs
    /// for the run's facility/report so the detail modal can paginate.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DataAcquisitionLogs(
        Guid id,
        int pageNumber = 1,
        int pageSize = 50,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        var facilityId = run.FacilityId;
        var reportId = run.ReportId;

        var allowedSortBy = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ExecutionDate", "CreateDate", "CompletionDate", "FacilityId", "PatientId",
            "QueryType", "QueryPhase", "Status", "Priority", "Id", "RetryAttempts",
            "IsDeleted", "ReportTrackingId"
        };
        if (!allowedSortBy.Contains(sortBy))
            sortBy = "Id";

        sortOrder = string.Equals(sortOrder, "Descending", StringComparison.OrdinalIgnoreCase)
            ? "Descending"
            : "Ascending";

        // Trim to keep server-side LIKE %term% predictable; whitespace-only terms collapse
        // to no-search.
        var normalizedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId))
            return Json(new { records = Array.Empty<object>(), metadata = new { totalCount = 0 } });

        try
        {
            var result = await dataAcqClient.SearchAcquisitionLogsAsync(
                facilityId,
                reportId,
                pageSize,
                pageNumber,
                sortBy,
                sortOrder,
                normalizedSearchTerm,
                cancellationToken);

            if ((result?.Body?.Records?.Count ?? 0) == 0)
            {
                result = await dataAcqClient.SearchAcquisitionLogsAsync(
                    string.Empty,
                    reportId,
                    pageSize,
                    pageNumber,
                    sortBy,
                    sortOrder,
                    normalizedSearchTerm,
                    cancellationToken);
            }

            var records = (result?.Body?.Records ?? [])
                .Select(r => new
                {
                    r.Id,
                    r.PatientId,
                    Status = r.Status?.ToString(),
                    QueryPhase = r.QueryPhase?.ToString(),
                    IsReferenceLog = r.IsReferenceLog
                                     || string.Equals(r.QueryPhase?.ToString(), "Referential", StringComparison.OrdinalIgnoreCase)
                                     || r.ReferenceResourceCount > 0,
                    r.ReferenceResourceCount,
                    ResourceTypes = (r.ResourceTypes ?? [])
                        .Concat(r.FhirQuery.SelectMany(q => q.ResourceTypes ?? []))
                        .Where(rt => !string.IsNullOrWhiteSpace(rt))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .ToList();

            var metadata = new
            {
                TotalCount = result?.Body?.Metadata?.TotalCount ?? 0,
                PageNumber = result?.Body?.Metadata?.PageNumber ?? pageNumber,
                PageSize = result?.Body?.Metadata?.PageSize ?? pageSize,
                TotalPages = result?.Body?.Metadata?.TotalPages ?? 0
            };

            return Json(new { records, metadata });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load DA logs for run {RunId} (facility={FacilityId}, report={ReportId})",
                id,
                facilityId,
                reportId);

            return Json(new { records = Array.Empty<object>(), metadata = new { totalCount = 0 } });
        }
    }

    /// <summary>
    /// Proxy endpoint for a single DA log detail used by the modal flyout.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DataAcquisitionLogDetail(
        Guid id,
        long logId,
        CancellationToken cancellationToken = default)
    {
        var run = await runManager.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        try
        {
            var detailed = await dataAcqClient.GetAcquisitionLogByIdAsync(logId, cancellationToken);
            if (detailed == null)
                return NotFound();

            // Fetch reference resources linked to this log.
            var referenceResourceIds = new List<string>();
            try
            {
                var pageNum = 1;
                const int refPageSize = 100;
                while (true)
                {
                    var refPage = await dataAcqClient.GetReferenceResourcesForLogAsync(logId, refPageSize, pageNum, cancellationToken);
                    var refRecords = refPage?.Body?.Records ?? [];
                    if (refRecords.Count == 0)
                        break;

                    referenceResourceIds.AddRange(
                        refRecords
                            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
                            .Select(r => $"{r.ResourceType}/{r.ResourceId}"));

                    if (refRecords.Count < refPageSize)
                        break;
                    pageNum++;
                }
            }
            catch (Exception refEx)
            {
                logger.LogWarning(refEx, "Failed to load reference resources for log {LogId}", logId);
            }

            // Build the human-readable "Resource?param=value&..." form per FhirQuery.
            // Mirrors the FhirQueryModel.Query getter so the UI shows what was actually
            // sent to the FHIR server.
            var queries = (detailed.Body?.FhirQuery ?? [])
                .Select(q =>
                {
                    var firstResource = q.ResourceTypes?.FirstOrDefault();
                    var paramJoin = string.Join("&", q.QueryParameters ?? []);
                    return q.QueryType switch
                    {
                        FhirQueryType.Search       => string.IsNullOrEmpty(firstResource) ? string.Empty : $"{firstResource}?{paramJoin}",
                        FhirQueryType.SearchPost   => string.IsNullOrEmpty(firstResource) ? string.Empty : $"{firstResource}/_search [{string.Join(",", q.QueryParameters ?? [])}]",
                        FhirQueryType.Read         => string.IsNullOrEmpty(firstResource) ? string.Empty : $"{firstResource}/{paramJoin}",
                        FhirQueryType.BulkDataPoll => paramJoin,
                        FhirQueryType.BulkDataRequest => "BulkDataRequest",
                        _ => string.Empty
                    };
                })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var queryType = (detailed.Body?.FhirQuery ?? [])
                .Select(q => q.QueryType.ToString())
                .FirstOrDefault();

            return Json(new
            {
                detailed.Body?.Id,
                detailed.Body?.PatientId,
                Status = detailed.Body?.Status?.ToString(),
                QueryPhase = detailed.Body?.QueryPhase?.ToString(),
                IsReferenceLog = detailed.Body?.IsReferenceLog == true
                                 || string.Equals(detailed.Body?.QueryPhase?.ToString(), "Referential", StringComparison.OrdinalIgnoreCase)
                                 || (detailed.Body?.ReferenceResourceCount ?? 0) > 0,
                ReferenceResourceCount = detailed.Body?.ReferenceResourceCount ?? 0,
                detailed.Body?.ReportTrackingId,
                detailed.Body?.CorrelationId,
                detailed.Body?.TraceId,
                detailed.Body?.FhirVersion,
                detailed.Body?.Priority,
                detailed.Body?.RetryAttempts,
                QueryType = queryType,
                Queries = queries,
                detailed.Body?.CompletionDate,
                CompletionTimeMilliseconds = detailed.Body?.CompletionTimeMilliseconds,
                ResourceTypes = (detailed.Body?.ResourceTypes ?? [])
                    .Concat((detailed.Body?.FhirQuery ?? []).SelectMany(q => q.ResourceTypes ?? []))
                    .Concat(referenceResourceIds
                        .Select(r => r.Contains('/') ? r.Split('/')[0] : r)
                        .Where(rt => !string.IsNullOrWhiteSpace(rt)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ResourceAcquiredIds = detailed.Body?.ResourceAcquiredIds?.ToList() ?? new List<string>(),
                ReferenceResourceIds = referenceResourceIds,
                Notes = detailed.Body?.Notes?.ToList() ?? new List<string>()
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load DA log detail for run {RunId}, log {LogId}", id, logId);
            return NotFound();
        }
    }
}
