using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class RunsController(
    IAutomationRunManager runManager,
    IScenarioStore scenarioStore,
    IQueryPlanTemplateStore queryPlanTemplateStore,
    IDataAcquisitionServiceClient dataAcqClient,
    ILogger<RunsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var stats = await runManager.GetDashboardStatsAsync(cancellationToken);
        var recentPage = await runManager.GetRunsPageAsync(1, 10, cancellationToken);
        var scenarios = (await scenarioStore.GetAllAsync(cancellationToken))
            .OrderBy(s => s.IsSystemScenario)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeRuns = recentPage.Runs
            .Where(r => r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running)
            .ToList();

        // Populate query plan templates for the shared scenario editor modal embedded in this view.
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(cancellationToken);

        var vm = new RunDashboardViewModel
        {
            Stats = stats,
            RecentRuns = recentPage.Runs,
            ActiveRuns = activeRuns,
            SavedScenarios = scenarios,
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DashboardStats(CancellationToken cancellationToken)
    {
        var stats = await runManager.GetDashboardStatsAsync(cancellationToken);
        var recentPage = await runManager.GetRunsPageAsync(1, 10, cancellationToken);

        var activeRuns = recentPage.Runs
            .Where(r => r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running)
            .ToList();

        return Json(new
        {
            stats,
            recentRuns = recentPage.Runs,
            activeRuns
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartScenarioRequest request, CancellationToken cancellationToken)
    {
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
                cancellationToken);

            if ((result?.Records?.Count ?? 0) == 0)
            {
                result = await dataAcqClient.SearchAcquisitionLogsAsync(
                    string.Empty,
                    reportId,
                    pageSize,
                    pageNumber,
                    sortBy,
                    sortOrder,
                    cancellationToken);
            }

            var records = (result?.Records ?? [])
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
                TotalCount = result?.Metadata?.TotalCount ?? 0,
                PageNumber = result?.Metadata?.PageNumber ?? pageNumber,
                PageSize = result?.Metadata?.PageSize ?? pageSize,
                TotalPages = result?.Metadata?.TotalPages ?? 0
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
                    var refRecords = refPage?.Records ?? [];
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

            return Json(new
            {
                detailed.Id,
                detailed.PatientId,
                Status = detailed.Status?.ToString(),
                QueryPhase = detailed.QueryPhase?.ToString(),
                IsReferenceLog = detailed.IsReferenceLog
                                 || string.Equals(detailed.QueryPhase?.ToString(), "Referential", StringComparison.OrdinalIgnoreCase)
                                 || detailed.ReferenceResourceCount > 0,
                ReferenceResourceCount = detailed.ReferenceResourceCount,
                detailed.ReportTrackingId,
                detailed.CorrelationId,
                detailed.CompletionDate,
                detailed.CompletionTimeMilliseconds,
                ResourceTypes = (detailed.ResourceTypes ?? [])
                    .Concat(detailed.FhirQuery.SelectMany(q => q.ResourceTypes ?? []))
                    .Concat(referenceResourceIds
                        .Select(r => r.Contains('/') ? r.Split('/')[0] : r)
                        .Where(rt => !string.IsNullOrWhiteSpace(rt)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ResourceAcquiredIds = detailed.ResourceAcquiredIds?.ToList() ?? new List<string>(),
                ReferenceResourceIds = referenceResourceIds,
                Notes = detailed.Notes?.ToList() ?? new List<string>()
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load DA log detail for run {RunId}, log {LogId}", id, logId);
            return NotFound();
        }
    }
}
