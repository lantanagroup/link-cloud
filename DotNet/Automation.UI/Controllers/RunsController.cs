using Automation.UI.Models;
using Automation.UI.Services;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

[Authorize]
public class RunsController(
    IAutomationRunManager runManager,
    IDataAcquisitionServiceClient dataAcqClient,
    ILogger<RunsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var runs = await runManager.GetRunsPageAsync(pageNumber, pageSize, cancellationToken);
        return View(runs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartScenarioRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index));

        var runId = await runManager.StartAsync(request, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = runId });
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
                                     || string.Equals(r.QueryPhase?.ToString(), "Referential", StringComparison.OrdinalIgnoreCase),
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
                    .Where(rt => !string.IsNullOrWhiteSpace(rt))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ResourceAcquiredIds = detailed.ResourceAcquiredIds?.ToList() ?? new List<string>(),
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
