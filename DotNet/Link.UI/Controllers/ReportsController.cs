using System.Text;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Link.UI.Controllers;

public sealed class ReportsController : Controller
{
    private readonly ReportsService _reports;
    private readonly FacilityViewService _view;
    private readonly AutomationOwnershipLookup _ownership;
    private readonly IOptions<LinkUiFeatureOptions> _features;
    private readonly KafkaOpsFixture _fixture;
    private readonly PatientSubmissionReader _submission;

    public ReportsController(
        ReportsService reports,
        FacilityViewService view,
        AutomationOwnershipLookup ownership,
        IOptions<LinkUiFeatureOptions> features,
        KafkaOpsFixture fixture,
        PatientSubmissionReader submission)
    {
        _reports = reports;
        _view = view;
        _ownership = ownership;
        _features = features;
        _fixture = fixture;
        _submission = submission;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ReportsListQuery query, CancellationToken cancellationToken)
    {
        query ??= new ReportsListQuery();
        ReportsListModel page;
        if (!_features.Value.AutomationEnabled)
        {
            query.Scope = null;
            page = await _reports.LoadListAsync(query, cancellationToken);
        }
        else
        {
            query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
            var (ownership, ownershipReachable) = await _ownership.GetSnapshotAsync(cancellationToken);
            var facility = string.IsNullOrWhiteSpace(query.FacilityId) ? null : query.FacilityId.Trim();
            if (AutomationMarkRules.IsAutomation(query.Scope))
            {
                if (facility is not null && !ownership.Contains(facility))
                {
                    page = EmptyReports(query, AutomationMarkRules.NotOwnedNote);
                }
                else if (facility is null)
                {
                    var ids = ownership.NewestFacilityIds(AutomationMarkRules.MaxFacilitySearches, out var truncated);
                    page = await _reports.LoadForFacilitiesAsync(query, ids, truncated, cancellationToken);
                }
                else
                {
                    page = await _reports.LoadListAsync(query, cancellationToken);
                }
            }
            else if (facility is not null && ownership.Contains(facility) && AutomationMarkRules.IsReal(query.Scope))
            {
                page = EmptyReports(query, AutomationMarkRules.OwnedFacilityNote);
            }
            else
            {
                page = await _reports.LoadListAsync(query, cancellationToken);
                if (AutomationMarkRules.IsReal(query.Scope))
                {
                    page.Reports = AutomationMarkRules.DropOwned(page.Reports, report => report.FacilityId, ownership, out var hidAny);
                    page.ScopeNote = AutomationMarkRules.WithHiddenNote(page.ScopeNote, hidAny);
                }
            }

            foreach (var report in page.Reports)
            {
                report.AutomationRunId = ownership.RunIdFor(report.FacilityId);
                report.CanResubmit = FacilityViewRules.CanResubmit(
                    report.Status,
                    report.Deleted,
                    !ownershipReachable || ownership.Contains(report.FacilityId));
            }
        }

        ViewData["Title"] = "Reports";
        return View(page);
    }

    private static ReportsListModel EmptyReports(ReportsListQuery query, string note) => new()
    {
        Query = query,
        ScopeNote = note,
        Paging = new PageBar { Page = 1, PageSize = FacilityViewRules.ClampPageSize(query.PageSize) }
    };

    [HttpGet]
    public async Task<IActionResult> Counts(string? ids, CancellationToken cancellationToken)
    {
        return Json(await _reports.LoadCountsAsync(ids, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        var page = await _reports.LoadGenerateAsync(cancellationToken);
        ViewData["Title"] = "Generate report";
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(GenerateReportInput input, CancellationToken cancellationToken)
    {
        var page = await _reports.GenerateAsync(input, cancellationToken);
        if (page.GeneratedReportId is Guid reportId)
        {
            TempData["Message"] = "Report generation requested.";
            return RedirectToAction("Report", "Tenants", new { id = page.Input.FacilityId, reportId });
        }

        ViewData["Title"] = "Generate report";
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Download(string? facilityId, string? reportId, CancellationToken cancellationToken)
    {
        var fileId = Guid.TryParse(reportId, out var parsed) ? parsed.ToString() : "report";
        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileId}.zip\"";
        var result = await _reports.CopyDownloadAsync(facilityId, reportId, Response.Body, cancellationToken);
        if (result.Succeeded || Response.HasStarted)
            return new EmptyResult();

        TempData["Error"] = result.Message;
        return RedirectToAction("Report", "Tenants", new { id = facilityId, reportId });
    }

    [HttpGet]
    public IActionResult Prequal()
    {
        var query = string.Join("&", Request.Query.SelectMany(pair =>
            pair.Value.Select(value =>
                Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(value ?? string.Empty))));
        var target = query.Length == 0 ? "/Reports/Validation" : "/Reports/Validation?" + query;
        return LocalRedirect(target);
    }

    [HttpGet]
    public async Task<IActionResult> Validation(
        string? facilityId,
        string? reportId,
        string? q,
        string? severity,
        string? code,
        string? category,
        string? sort,
        string? dir,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ReportsRules.NormalizeIssueQuery(q, severity, code, category, sort, dir, page, pageSize);
        var model = await _reports.LoadValidationPageAsync(facilityId, reportId, query, cancellationToken);
        ViewData["Title"] = "Validation";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Measure(
        string? facilityId,
        string? reportId,
        string? patientId,
        bool json,
        CancellationToken cancellationToken)
    {
        var page = await _reports.LoadMeasureAsync(facilityId, reportId, patientId, json, cancellationToken);
        ViewData["Title"] = "Measure report";
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> MeasureFile(string? facilityId, string? reportId, string? patientId, CancellationToken cancellationToken)
    {
        var read = await _reports.ReadPatientBundleAsync(facilityId, reportId, patientId, cancellationToken);
        if (!read.Action.Succeeded || read.Body is null)
        {
            TempData["Error"] = read.Action.Message;
            return RedirectToAction(nameof(Measure), new { facilityId, reportId, patientId });
        }

        return File(Encoding.UTF8.GetBytes(read.Body), "application/fhir+json", "measure-eval-input.json");
    }

    [HttpGet]
    public async Task<IActionResult> Acquisition(
        string? facilityId,
        string? reportId,
        string? patientId,
        CancellationToken cancellationToken)
    {
        var model = await _reports.LoadSectionAsync(facilityId, reportId, cancellationToken);
        ViewData["Title"] = "Acquisition log";
        ViewData["PatientId"] = FacilityViewRules.Clean(patientId);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Manifest(
        string? facilityId,
        string? reportId,
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
        string? stage,
        string? stageMeasure,
        int popPage,
        string? tab,
        CancellationToken cancellationToken)
    {
        var query = ReportManifestRules.Normalize(
            q, sort, dir, typeQ, cmpQ, page, pageSize, typePage, typeSize, cmpPage, cmpSize, cmpType, "status",
            stage, stageMeasure, popPage, tab);
        var returnUrl = ReturnUrlRules.FromQuery(Request);
        ReportManifestPage model;
        var scale = _fixture.Active && string.Equals(Request.Query["scale"], "1", StringComparison.Ordinal);
        if (_fixture.Active && ReportManifestRules.IsSample(facilityId, reportId))
        {
            model = scale
                ? ReportManifestRules.ScaleReport(query, returnUrl)
                : ReportManifestRules.SampleReport(query, returnUrl);
        }
        else
        {
            var route = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(facilityId))
                route["facilityId"] = facilityId.Trim();
            if (!string.IsNullOrWhiteSpace(reportId))
                route["reportId"] = reportId.Trim();
            if (!string.IsNullOrWhiteSpace(returnUrl))
                route["returnUrl"] = returnUrl;

            model = await _reports.LoadManifestAsync(
                facilityId,
                reportId,
                query,
                "/Reports/Manifest",
                route,
                patientId => PatientMeasureHref(facilityId, reportId, patientId, returnUrl),
                cancellationToken);
        }

        ViewData["Title"] = "Report manifest";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PatientGraph(
        string? facilityId,
        string? reportId,
        string? patientId,
        string? part,
        string? type,
        string? q,
        string? id,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var patient = (patientId ?? string.Empty).Sanitize().Trim();
        if (patient.Length is 0 or > 80)
            return Problem(detail: "A patient id is required.", statusCode: StatusCodes.Status400BadRequest);

        var facility = (facilityId ?? string.Empty).Sanitize().Trim();
        var report = (reportId ?? string.Empty).Sanitize().Trim();
        var scale = _fixture.Active
            && ReportManifestRules.IsSample(facility, report)
            && string.Equals(Request.Query["scale"], "1", StringComparison.Ordinal);
        var key = facility + "|" + report + "|" + patient + "|" + (scale ? "1" : "0");
        var index = ResourceGraphRules.Recall(key);
        var sample = _fixture.Active && ReportManifestRules.IsSample(facility, report);
        if (index is null && !sample)
        {
            var streamed = await TrySubmissionAsync(facility, report, patient, which: (part ?? "summary").Sanitize().Trim().ToLowerInvariant(), key, cancellationToken);
            if (streamed.Handled)
                return streamed.Result!;
            index = streamed.Index;
        }

        if (index is null)
        {
            var loaded = await LoadGraphAsync(facility, report, patient, scale, cancellationToken);
            if (loaded.Problem is not null)
                return loaded.Problem;
            index = loaded.Index!;
            ResourceGraphRules.Remember(key, index);
        }

        if (!string.IsNullOrWhiteSpace(index.Error) && index.Total == 0)
            return Problem(detail: index.Error, statusCode: StatusCodes.Status400BadRequest);

        var which = (part ?? "summary").Sanitize().Trim().ToLowerInvariant();
        var typeName = Bound(type, 64);
        var query = Bound(q, 80);
        var resourceId = Bound(id, 80);
        if (which == "page")
        {
            if (query.Length > 0 && index.Address is not null && !index.HasSearchableBodies)
            {
                var streamed = await _submission.PageAsync(
                    index.Address, index.PatientId, typeName, query, page, pageSize, cancellationToken);
                if (streamed is not null)
                    return Json(streamed);
            }

            return Json(index.Page(typeName, query, page, pageSize, cancellationToken));
        }
        if (which == "raw")
        {
            var raw = index.Raw(typeName, resourceId);
            if (raw is null)
                return Problem(detail: "That resource is not in this graph.", statusCode: StatusCodes.Status404NotFound);
            if (raw.Json is null
                && index.Address is not null
                && index.TrySpan(typeName, resourceId, out var offset, out var length))
            {
                var body = await _submission.ReadBodyAsync(index.Address, offset, length, cancellationToken);
                raw = ResourceGraphIndex.WithBody(raw, body);
            }

            return Json(raw);
        }

        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-store";
        await ResourceGraphRules.SummarizeAsync(index, Response.Body, cancellationToken);
        return new EmptyResult();
    }

    private async Task<(bool Handled, IActionResult? Result, ResourceGraphIndex? Index)> TrySubmissionAsync(
        string facilityId,
        string reportId,
        string patientId,
        string which,
        string key,
        CancellationToken cancellationToken)
    {
        if (!_submission.IsConfigured)
            return (false, null, null);

        LantanaGroup.Link.Shared.Application.Models.Integration.Report.ReportScheduleApiModel? schedule;
        try
        {
            schedule = await _reports.TryScheduleAsync(facilityId, reportId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (false, null, null);
        }

        if (schedule is null)
            return (false, null, null);

        var summary = which == "summary";
        var started = false;
        try
        {
            var built = await _submission.ReadAsync(
                schedule,
                patientId,
                summary
                    ? ct =>
                    {
                        Response.ContentType = "application/x-ndjson; charset=utf-8";
                        Response.Headers.CacheControl = "no-store";
                        started = true;
                        return Task.CompletedTask;
                    }
                    : null,
                summary
                    ? (read, ct) => new ValueTask(ResourceGraphRules.WriteProgressAsync(Response.Body, read, ct))
                    : null,
                cancellationToken);
            if (built is null)
                return (false, null, null);

            ResourceGraphRules.Remember(key, built);
            if (!summary)
                return (false, null, built);

            if (!started)
            {
                Response.ContentType = "application/x-ndjson; charset=utf-8";
                Response.Headers.CacheControl = "no-store";
            }

            await ResourceGraphRules.WriteDoneAsync(built, Response.Body, cancellationToken);
            return (true, new EmptyResult(), built);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (!started)
                return (false, null, null);

            var failed = ResourceGraphRules.Unavailable(patientId, "The submission could not be read.");
            try
            {
                await ResourceGraphRules.WriteDoneAsync(failed, Response.Body, cancellationToken);
            }
            catch (Exception)
            {
                // The response already started. There is no second chance to change the status.
            }

            return (true, new EmptyResult(), null);
        }
    }

    private async Task<(ResourceGraphIndex? Index, IActionResult? Problem)> LoadGraphAsync(
        string facilityId,
        string reportId,
        string patientId,
        bool scale,
        CancellationToken cancellationToken)
    {
        if (_fixture.Active && ReportManifestRules.IsSample(facilityId, reportId))
        {
            if (!ResourceGraphRules.TryFixture(patientId, scale, out var spec))
                return (null, Problem(detail: "That patient is not on this report.", statusCode: StatusCodes.Status404NotFound));
            return (ResourceGraphRules.Build(spec, cancellationToken), null);
        }

        try
        {
            var read = await _reports.ReadPatientBundleAsync(facilityId, reportId, patientId, cancellationToken);
            if (!read.Action.Succeeded || string.IsNullOrWhiteSpace(read.Body))
            {
                var message = (read.Action.Message ?? string.Empty).Sanitize().Trim();
                var status = message.Contains("not on this report", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                        ? StatusCodes.Status503ServiceUnavailable
                        : StatusCodes.Status502BadGateway;
                if (message.Length == 0)
                    message = "The bundle could not be read.";
                return (null, Problem(detail: message, statusCode: status));
            }

            var index = ResourceGraphRules.ReadBundle(read.Body, read.PatientId ?? patientId, cancellationToken);
            return (index, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (null, Problem(detail: "MeasureEval could not be read.", statusCode: StatusCodes.Status502BadGateway));
        }
    }

    private static string Bound(string? value, int max)
    {
        var text = (value ?? string.Empty).Sanitize().Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string PatientMeasureHref(string? facilityId, string? reportId, string patientId, string? returnUrl)
    {
        var parts = new List<string>
        {
            "facilityId=" + Uri.EscapeDataString(facilityId?.Trim() ?? string.Empty),
            "reportId=" + Uri.EscapeDataString(reportId?.Trim() ?? string.Empty),
            "patientId=" + Uri.EscapeDataString(patientId ?? string.Empty)
        };
        if (!string.IsNullOrWhiteSpace(returnUrl))
            parts.Add("returnUrl=" + Uri.EscapeDataString(returnUrl));
        return "/Reports/Measure?" + string.Join("&", parts);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Resubmit(string? rowFacility, string? targetId, bool bypassSubmission, ReportsListQuery query, CancellationToken cancellationToken) =>
        Act(query, () => _view.ResubmitAsync(rowFacility, targetId, bypassSubmission, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Abort(string? rowFacility, string? targetId, ReportsListQuery query, CancellationToken cancellationToken) =>
        Act(query, () => _view.AbortAsync(rowFacility, targetId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CleanUp(string? rowFacility, string? targetId, ReportsListQuery query, CancellationToken cancellationToken) =>
        Act(query, () => _view.CleanUpAsync(rowFacility, targetId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(string? rowFacility, string? targetId, ReportsListQuery query, CancellationToken cancellationToken) =>
        Act(query, () => _view.RestoreReportAsync(rowFacility, targetId, cancellationToken));

    private async Task<IActionResult> Act(ReportsListQuery query, Func<Task<FacilityViewAction>> action)
    {
        var result = await action();
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index), query.ToRoute());
    }
}
