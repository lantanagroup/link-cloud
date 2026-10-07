using System.Text;
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

    public ReportsController(
        ReportsService reports,
        FacilityViewService view,
        AutomationOwnershipLookup ownership,
        IOptions<LinkUiFeatureOptions> features)
    {
        _reports = reports;
        _view = view;
        _ownership = ownership;
        _features = features;
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
            var ownership = await _ownership.GetAsync(cancellationToken);
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
                report.AutomationRunId = ownership.RunIdFor(report.FacilityId);
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
    public async Task<IActionResult> Prequal(string? facilityId, string? reportId, string? category, CancellationToken cancellationToken)
    {
        var page = await _reports.LoadPrequalAsync(facilityId, reportId, category, cancellationToken);
        ViewData["Title"] = "Prequalification";
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Validation(string? facilityId, string? reportId, CancellationToken cancellationToken)
    {
        var page = await _reports.LoadValidationAsync(facilityId, reportId, cancellationToken);
        ViewData["Title"] = "Validation";
        return View(page);
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
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var model = await _reports.LoadAcquisitionAsync(facilityId, reportId, patientId, page, pageSize, cancellationToken);
        ViewData["Title"] = "Acquisition log";
        return View(model);
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
