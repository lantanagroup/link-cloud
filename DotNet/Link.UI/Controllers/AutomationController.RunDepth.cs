using System.Text;
using Automation.UI.Models;
using Automation.UI.Models.Metrics;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed partial class AutomationController
{
    [HttpGet("pipeline-snapshot")]
    public async Task<IActionResult> PipelineSnapshot(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        var snapshot = await _manager!.GetPipelineSnapshotAsync(id, cancellationToken);
        if (snapshot == null)
            return NoContent();

        return Json(snapshot);
    }

    [HttpGet("manifest")]
    public async Task<IActionResult> Manifest(
        Guid id,
        string? q,
        string? sort,
        string? dir,
        string? typeQ,
        string? cmpQ,
        int page,
        int pageSize,
        int typePage,
        int typeSize,
        int cmpPage,
        int cmpSize,
        int cmpType,
        CancellationToken cancellationToken)
    {
        var query = ReportManifestRules.Normalize(
            q, sort, dir, typeQ, cmpQ, page, pageSize, typePage, typeSize, cmpPage, cmpSize, cmpType, "total");
        const string path = "/Automation/manifest";
        var fixture = _services.GetService<KafkaOpsFixture>();
        if (id == ReportManifestRules.SampleId && (!_engine.Ready || _manager is null) && fixture?.Active == true)
        {
            ViewData["Title"] = "Generation manifest";
            ViewData["AutomationSection"] = "runs";
            return View(
                "~/Views/Automation/Manifest.cshtml",
                ReportManifestRules.SampleAutomation(query, path, Url.Action(nameof(DownloadGeneratedBundle), new { id })));
        }

        if (EngineOff() is { } off)
            return off;

        var run = await _manager!.GetRunForDisplayAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        var manifest = await _manager.GetGenerationManifestAsync(id, cancellationToken);
        if (manifest == null)
            return RedirectToAction(nameof(Run), new { id });

        AbsUploadSnapshot? actual = null;
        if (run.Status.IsTerminal())
            actual = await _manager.GetAbsUploadSnapshotAsync(id, cancellationToken);

        await FillRunDetailAsync(run, cancellationToken);
        var latest = await LatestTemplateCacheVersionAsync(run.GeneratedTemplateCacheScenarioKey, cancellationToken);
        var model = ReportManifestRules.FromGeneration(
            manifest,
            actual,
            query,
            showComparison: run.Status.IsTerminal(),
            id,
            path,
            Url.Action(nameof(DownloadGeneratedBundle), new { id }),
            run.RunConfigurationJson,
            ConfigurationNames(),
            run.GeneratedTemplateCacheVersionNumber,
            latest);

        ViewData["Title"] = "Generation manifest";
        ViewData["AutomationSection"] = "runs";
        return View("~/Views/Automation/Manifest.cshtml", model);
    }

    [HttpGet("manifest-data")]
    public async Task<IActionResult> ManifestData(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        var manifest = await _manager!.GetGenerationManifestAsync(id, cancellationToken);
        if (manifest == null)
            return NoContent();

        return Json(manifest);
    }

    [HttpGet("abs-upload")]
    public async Task<IActionResult> AbsUploadData(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        var abs = await _manager!.GetAbsUploadSnapshotAsync(id, cancellationToken);
        if (abs == null)
            return NoContent();

        return Json(abs);
    }

    [HttpGet("generated-bundle")]
    public async Task<IActionResult> DownloadGeneratedBundle(
        Guid id,
        string? patientId,
        CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        var replay = _services.GetService<GeneratedPatientBundleReplayService>();
        if (replay is null)
            return EngineOff()!;

        var run = await _manager!.GetRunForDisplayAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        var patient = (patientId ?? string.Empty).Sanitize().Trim();
        if (string.IsNullOrWhiteSpace(patient))
            return BadRequest("Patient ID is required.");

        var manifest = await _manager.GetGenerationManifestAsync(id, cancellationToken);
        var result = await replay.ReplayAsync(run, manifest, patient, cancellationToken);
        if (!result.Found || string.IsNullOrWhiteSpace(result.BundleJson))
            return NotFound(result.Error ?? "Generated bundle is not available for this patient.");

        if (result.RunCacheVersion.HasValue)
            Response.Headers["X-Generation-Template-Version"] = result.RunCacheVersion.Value.ToString();
        if (result.LatestCacheVersion.HasValue)
            Response.Headers["X-Generation-Template-Latest"] = result.LatestCacheVersion.Value.ToString();
        if (result.GenerationChanged)
            Response.Headers["X-Generation-Template-Stale"] = "true";

        return File(Encoding.UTF8.GetBytes(result.BundleJson), "application/fhir+json", result.FileName);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        var export = _services.GetService<IRunExportService>();
        if (export is null)
            return EngineOff()!;

        var run = await _manager!.GetRunForDisplayAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        if (!run.Status.IsTerminal())
        {
            return Conflict(new { error = "Run must be completed (Succeeded, Failed, or Cancelled) before it can be exported." });
        }

        var package = await export.BuildAsync(id, cancellationToken);
        if (package == null)
            return NotFound();

        return new DeleteAfterSendFileResult(package.FilePath, "application/zip", package.FileName);
    }

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelJson([FromBody] RunActionRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { success = false, error = "Missing run ID" });

        try
        {
            var cancelled = await _manager!.CancelRunAsync(request.Id, cancellationToken);
            if (cancelled)
                return Ok(new { success = true });

            return Ok(new { success = false, error = "This run is not running." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel failed for run {RunId}.", request.Id);
            return Problem(
                detail: "Cancel could not be completed. Refresh the page and check the run status.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteJson([FromBody] RunActionRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { success = false, error = "Missing run ID" });

        var deleted = await _manager!.DeleteRunAsync(request.Id, cancellationToken);
        return Ok(new { success = deleted });
    }

    [HttpPost("admit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdmitJson([FromBody] LiveInjectRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing run ID" });
        if (string.IsNullOrWhiteSpace(request.PatientId))
            return BadRequest(new { error = "patientId is required." });

        try
        {
            var evt = await _manager!.InjectAdmitAsync(request.Id, request.PatientId.Sanitize(), "UI", request.Notes.Sanitize(), cancellationToken);
            return Ok(evt);
        }
        catch (LiveInjectionException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("discharge")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DischargeJson([FromBody] LiveInjectRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing run ID" });
        if (string.IsNullOrWhiteSpace(request.PatientId))
            return BadRequest(new { error = "patientId is required." });

        try
        {
            var evt = await _manager!.InjectDischargeAsync(request.Id, request.PatientId.Sanitize(), "UI", request.Notes.Sanitize(), cancellationToken);
            return Ok(evt);
        }
        catch (LiveInjectionException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("live-events")]
    public async Task<IActionResult> LiveEvents(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (await _manager!.GetRunForDisplayAsync(id, cancellationToken) == null)
            return NotFound();

        return Json(await _manager.GetLiveEventsAsync(id, cancellationToken));
    }

    [HttpGet("live-patient-state")]
    public async Task<IActionResult> LivePatientState(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (await _manager!.GetRunForDisplayAsync(id, cancellationToken) == null)
            return NotFound();

        return Json(await _manager.GetLivePatientStateAsync(id, cancellationToken));
    }

    [HttpPost("pool-generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneratePoolJson([FromBody] RunActionRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing run ID" });

        try
        {
            return Ok(await _manager!.GenerateLivePoolPatientAsync(request.Id, "UI", cancellationToken));
        }
        catch (LiveInjectionException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("pool-upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPoolJson([FromBody] LivePoolUploadRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing run ID" });
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Upload content is required." });

        try
        {
            return Ok(await _manager!.UploadLivePoolPatientAsync(
                request.Id,
                request.Content,
                request.FileName.Sanitize(),
                "UI",
                cancellationToken));
        }
        catch (LiveInjectionException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("pool-reference")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReferencePoolJson([FromBody] LivePoolReferenceRequest? request, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing run ID" });
        if (string.IsNullOrWhiteSpace(request.PatientId))
            return BadRequest(new { error = "patientId is required." });

        try
        {
            return Ok(await _manager!.ReferenceLivePoolPatientAsync(request.Id, request.PatientId.Sanitize(), "UI", cancellationToken));
        }
        catch (LiveInjectionException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("performance-panel")]
    public async Task<IActionResult> PerformancePanel(Guid id, CancellationToken cancellationToken)
    {
        var presenter = _services.GetService<MetricsRunPresenter>();
        if (presenter is null)
            return NoContent();

        var detail = await presenter.GetCapturedAsync(id, cancellationToken);
        if (detail == null)
            return NoContent();

        return PartialView("~/Views/Automation/_AdvancedPerformance.cshtml", detail);
    }

    [HttpGet("acquisition-logs")]
    public async Task<IActionResult> DataAcquisitionLogs(
        Guid id,
        int pageNumber = 1,
        int pageSize = 50,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        if (EngineOff() is { } off)
            return off;

        var client = _services.GetService<IDataAcquisitionServiceClient>();
        if (client is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var run = await _manager!.GetRunForDisplayAsync(id, cancellationToken);
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

        var normalizedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Sanitize().Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearchTerm))
            normalizedSearchTerm = null;

        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId))
            return Json(new { records = Array.Empty<object>(), metadata = new { totalCount = 0 } });

        try
        {
            var result = await client.SearchAcquisitionLogsAsync(
                facilityId,
                reportId,
                pageSize,
                pageNumber,
                sortBy,
                sortOrder,
                normalizedSearchTerm,
                cancellationToken);

            if ((result?.Body?.Records?.Count ?? 0) == 0
                && (result?.Body != null || result?.IsSuccessStatusCode == true))
            {
                var fallback = await client.SearchAcquisitionLogsAsync(
                    string.Empty,
                    reportId,
                    pageSize,
                    pageNumber,
                    sortBy,
                    sortOrder,
                    normalizedSearchTerm,
                    cancellationToken);
                if (AcquisitionLogAvailability.PreferFallback(
                        result?.Body != null,
                        fallback?.Body?.Records?.Count ?? 0,
                        fallback?.Body != null))
                    result = fallback;
            }

            var read = AcquisitionLogAvailability.Classify(result?.StatusCode, result?.Body != null);
            if (read.Unavailable)
                return Json(new { records = Array.Empty<object>(), metadata = new { totalCount = 0 }, unavailable = true });
            if (read.ErrorStatus is int status)
                return StatusCode(status);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load data acquisition logs for run {RunId}.",
                id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("acquisition-log")]
    public async Task<IActionResult> DataAcquisitionLogDetail(
        Guid id,
        long logId,
        CancellationToken cancellationToken = default)
    {
        if (EngineOff() is { } off)
            return off;

        var client = _services.GetService<IDataAcquisitionServiceClient>();
        if (client is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var run = await _manager!.GetRunForDisplayAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        try
        {
            var detailed = await client.GetAcquisitionLogByIdAsync(logId, cancellationToken);
            var detailRead = AcquisitionLogAvailability.Classify(
                detailed?.StatusCode, detailed?.Body != null, notFoundIsMissing: true);
            if (detailRead.Unavailable)
                return Json(new { unavailable = true });
            if (detailRead.ErrorStatus is int detailStatus)
                return StatusCode(detailStatus);

            var referenceResourceIds = new List<string>();
            try
            {
                var pageNum = 1;
                const int refPageSize = 100;
                while (true)
                {
                    var refPage = await client.GetReferenceResourcesForLogAsync(logId, refPageSize, pageNum, cancellationToken);
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
                _logger.LogWarning(refEx, "Failed to load reference resources for acquisition log {LogId}.", logId);
            }

            var queries = (detailed!.Body?.FhirQuery ?? [])
                .Select(q =>
                {
                    var firstResource = q.ResourceTypes?.FirstOrDefault();
                    var paramJoin = string.Join("&", q.QueryParameters ?? []);
                    return q.QueryType switch
                    {
                        FhirQueryType.Search => string.IsNullOrEmpty(firstResource) ? string.Empty : $"{firstResource}?{paramJoin}",
                        FhirQueryType.SearchPost => string.IsNullOrEmpty(firstResource) ? string.Empty : $"{firstResource}/_search [{string.Join(",", q.QueryParameters ?? [])}]",
                        FhirQueryType.Read => string.IsNullOrEmpty(firstResource) ? string.Empty : $"{firstResource}/{paramJoin}",
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load acquisition log {LogId} for run {RunId}.", logId, id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs(Guid id, int pageNumber = 0, int pageSize = RunLogPaging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (EngineOff() is { } off)
            return off;

        var store = _services.GetService<ISnapshotStore>();
        if (store is null)
            return EngineOff()!;

        if (await _manager!.GetRunForDisplayAsync(id, cancellationToken) == null)
            return NotFound();

        return Json(await store.GetLogPageAsync(id, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("live-utilization")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LiveUtilization(CancellationToken cancellationToken)
    {
        var live = _services.GetService<ILiveProcessUtilizationService>();
        if (live is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        return Json(await live.GetAsync(cancellationToken));
    }

    private async Task FillScenarioEditorCatalogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var queryPlans = _services.GetService<IQueryPlanTemplateStore>();
            var normalizations = _services.GetService<INormalizationStore>();
            var maps = _services.GetService<IOrganizationResourceMapTemplateStore>();
            var patients = _services.GetService<IPatientConfigurationStore>();
            var measures = _services.GetService<IMeasureTemplateStore>();
            var facilities = _services.GetService<IFacilityTemplateStore>();
            if (queryPlans is not null)
                ViewBag.QueryPlanTemplates = await queryPlans.GetAllAsync(cancellationToken);
            if (normalizations is not null)
                ViewBag.NormalizationSuites = await normalizations.GetAllSuitesAsync(cancellationToken);
            if (maps is not null)
                ViewBag.OrganizationResourceMaps = await maps.GetAllAsync(cancellationToken);
            if (patients is not null)
                ViewBag.PatientConfigurations = await patients.GetAllAsync(cancellationToken);
            if (measures is not null)
                ViewBag.MeasureTemplates = await measures.GetAllAsync(cancellationToken);
            if (facilities is not null)
                ViewBag.FacilityTemplates = await facilities.GetAllAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scenario editor catalogs could not be loaded for the runs dashboard.");
        }
    }

    private async Task FillRunDetailAsync(LantanaGroup.Link.Automation.Link.Models.AutomationRunSummary detail, CancellationToken cancellationToken)
    {
        ViewBag.TemplateCacheVersionNumber = detail.GeneratedTemplateCacheVersionNumber;
        try
        {
            var patients = _services.GetService<IPatientConfigurationStore>();
            if (patients is not null)
                ViewBag.PatientConfigurations = await patients.GetAllAsync(cancellationToken);
            if (detail.IsMetricsRun)
            {
                var presenter = _services.GetService<MetricsRunPresenter>();
                if (presenter is not null)
                    ViewBag.Performance = await presenter.GetCapturedAsync(detail.RunId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Run detail catalogs could not be loaded for {RunId}.", detail.RunId);
        }
    }

    private async Task<int?> LatestTemplateCacheVersionAsync(string? scenarioKey, CancellationToken cancellationToken)
    {
        var store = _services.GetService<GeneratedTemplateCacheVersionStore>();
        if (store is null)
            return null;

        var latest = await store.GetLatestAsync(scenarioKey, cancellationToken);
        return latest?.VersionNumber;
    }

    private IReadOnlyDictionary<string, string> ConfigurationNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ViewBag.PatientConfigurations is not IEnumerable<PatientConfiguration> rows)
            return names;

        foreach (var row in rows)
        {
            if (row.Id == Guid.Empty || string.IsNullOrWhiteSpace(row.Name))
                continue;
            names[row.Id.ToString()] = row.Name.Trim();
        }

        return names;
    }

    private IActionResult? EngineOff()
    {
        if (_engine.Ready && _manager is not null)
            return null;

        return Problem(
            detail: _engine.Message ?? AutomationRunReader.NotConfiguredMessage,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    public sealed class RunActionRequest
    {
        public Guid Id { get; set; }
    }

    public sealed class LiveInjectRequest
    {
        public Guid Id { get; set; }

        public string? PatientId { get; set; }

        public string? Notes { get; set; }
    }

    public sealed class LivePoolUploadRequest
    {
        public Guid Id { get; set; }

        public string? Content { get; set; }

        public string? FileName { get; set; }
    }

    public sealed class LivePoolReferenceRequest
    {
        public Guid Id { get; set; }

        public string? PatientId { get; set; }
    }
}
