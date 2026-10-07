using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Link.UI.Controllers;

[Route("Logs")]
public sealed class LogsController : Controller
{
    private readonly LogsService _logs;
    private readonly AutomationOwnershipLookup _ownership;
    private readonly IOptions<LinkUiFeatureOptions> _features;

    public LogsController(LogsService logs, AutomationOwnershipLookup ownership, IOptions<LinkUiFeatureOptions> features)
    {
        _logs = logs;
        _ownership = ownership;
        _features = features;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Logs";
        return View(_logs.LoadHome());
    }

    [HttpGet("Acquisition")]
    public async Task<IActionResult> Acquisition(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Acquisition log";
        ViewData["LogsSection"] = "acquisition";
        return View(await AcquisitionPage(query, cancellationToken));
    }

    [HttpGet("Acquisition/{id:long}")]
    public async Task<IActionResult> AcquisitionDetail(long id, int refPage, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Acquisition log";
        ViewData["LogsSection"] = "acquisition";
        return View(await _logs.LoadAcquisitionDetailAsync(id, refPage, cancellationToken));
    }

    [HttpPost("Acquisition/process")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(AcquisitionQuery query, List<long>? ids, CancellationToken cancellationToken)
    {
        var result = await _logs.ProcessSelectedAsync(ids, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/process-matching")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessMatching(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        var rejected = RejectWideAutomation(query);
        if (rejected is not null)
            return rejected;
        var result = await _logs.ProcessMatchingAsync(query, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(AcquisitionQuery query, List<long>? ids, CancellationToken cancellationToken)
    {
        var result = await _logs.CancelSelectedAsync(ids, query.MinAgeHours, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/cancel-matching")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMatching(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        var rejected = RejectWideAutomation(query);
        if (rejected is not null)
            return rejected;
        var result = await _logs.CancelMatchingAsync(query, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/disable-facility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableFacility(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        var result = await _logs.ChangeFacilityLogsAsync(query.FacilityId, restore: false, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/restore-facility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreFacility(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        var result = await _logs.ChangeFacilityLogsAsync(query.FacilityId, restore: true, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/{id:long}/process")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessOne(long id, CancellationToken cancellationToken)
    {
        var result = await _logs.ProcessSelectedAsync([id], cancellationToken);
        Temp(result);
        return RedirectToAction(nameof(AcquisitionDetail), new { id });
    }

    [HttpPost("Acquisition/{id:long}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOne(long id, int minAgeHours, CancellationToken cancellationToken)
    {
        var result = await _logs.CancelSelectedAsync([id], minAgeHours, cancellationToken);
        Temp(result);
        return RedirectToAction(nameof(AcquisitionDetail), new { id });
    }

    [HttpGet("Sftp")]
    public async Task<IActionResult> Sftp(SftpQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "SFTP acquisition log";
        ViewData["LogsSection"] = "sftp";
        return View(await SftpPage(query, cancellationToken));
    }

    [HttpGet("Sftp/{id:guid}")]
    public async Task<IActionResult> SftpDetail(string id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "SFTP acquisition log";
        ViewData["LogsSection"] = "sftp";
        return View(await _logs.LoadSftpDetailAsync(id, cancellationToken));
    }

    [HttpPost("Sftp/{id:guid}/reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetSftp(string id, CancellationToken cancellationToken)
    {
        var result = await _logs.ResetSftpAsync(id, cancellationToken);
        Temp(result);
        return RedirectToAction(nameof(SftpDetail), new { id });
    }

    [HttpGet("Audit")]
    public async Task<IActionResult> Audit(AuditQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Audit event log";
        ViewData["LogsSection"] = "audit";
        return View(await AuditPage(query, cancellationToken));
    }

    [HttpGet("Audit/{id:guid}")]
    public async Task<IActionResult> AuditDetail(string id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Audit event";
        ViewData["LogsSection"] = "audit";
        return View(await _logs.LoadAuditDetailAsync(id, cancellationToken));
    }

    [HttpGet("Kafka")]
    public IActionResult Kafka()
    {
        ViewData["Title"] = "Kafka";
        ViewData["LogsSection"] = "kafka";
        return View(_logs.LoadKafka());
    }

    private IActionResult? RejectWideAutomation(AcquisitionQuery query)
    {
        if (!AutomationMarkRules.IsAutomation(query.Scope) || !string.IsNullOrWhiteSpace(query.FacilityId))
            return null;

        TempData["Error"] = "Choose one automation facility before changing every matching log.";
        return RedirectToAction(nameof(Acquisition), query.ToRoute());
    }

    private async Task<AcquisitionLogListPage> AcquisitionPage(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        query ??= new AcquisitionQuery();
        if (!_features.Value.AutomationEnabled)
        {
            query.Scope = null;
            return await _logs.LoadAcquisitionAsync(query, cancellationToken);
        }

        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var ownership = await _ownership.GetAsync(cancellationToken);
        var facility = string.IsNullOrWhiteSpace(query.FacilityId) ? null : query.FacilityId.Trim();
        AcquisitionLogListPage page;
        if (AutomationMarkRules.IsAutomation(query.Scope))
        {
            if (facility is not null && !ownership.Contains(facility))
                page = NotOwnedAcquisition(query);
            else if (facility is null)
            {
                var ids = ownership.NewestFacilityIds(AutomationMarkRules.MaxFacilitySearches, out var truncated);
                page = await _logs.LoadAcquisitionForFacilitiesAsync(query, ids, truncated, cancellationToken);
            }
            else
                page = await _logs.LoadAcquisitionAsync(query, cancellationToken);
        }
        else if (facility is not null && ownership.Contains(facility) && AutomationMarkRules.IsReal(query.Scope))
        {
            page = NotOwnedAcquisition(query);
            page.ScopeNote = AutomationMarkRules.OwnedFacilityNote;
        }
        else
        {
            page = await _logs.LoadAcquisitionAsync(query, cancellationToken);
            if (AutomationMarkRules.IsReal(query.Scope))
            {
                page.Logs = AutomationMarkRules.DropOwned(page.Logs, row => row.FacilityId, ownership, out var hidAny);
                page.ScopeNote = AutomationMarkRules.WithHiddenNote(page.ScopeNote, hidAny);
            }
        }

        foreach (var row in page.Logs)
            row.AutomationRunId = ownership.RunIdFor(row.FacilityId);
        return page;
    }

    private async Task<SftpLogListPage> SftpPage(SftpQuery query, CancellationToken cancellationToken)
    {
        query ??= new SftpQuery();
        if (!_features.Value.AutomationEnabled)
        {
            query.Scope = null;
            return await _logs.LoadSftpAsync(query, cancellationToken);
        }

        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var ownership = await _ownership.GetAsync(cancellationToken);
        var facility = string.IsNullOrWhiteSpace(query.FacilityId) ? null : query.FacilityId.Trim();
        SftpLogListPage page;
        if (AutomationMarkRules.IsAutomation(query.Scope))
        {
            if (facility is not null && !ownership.Contains(facility))
                page = EmptySftp(query, AutomationMarkRules.NotOwnedNote);
            else if (facility is null)
            {
                var ids = ownership.NewestFacilityIds(AutomationMarkRules.MaxFacilitySearches, out var truncated);
                page = await _logs.LoadSftpForFacilitiesAsync(query, ids, truncated, cancellationToken);
            }
            else
                page = await _logs.LoadSftpAsync(query, cancellationToken);
        }
        else if (facility is not null && ownership.Contains(facility) && AutomationMarkRules.IsReal(query.Scope))
        {
            page = EmptySftp(query, AutomationMarkRules.OwnedFacilityNote);
        }
        else
        {
            page = await _logs.LoadSftpAsync(query, cancellationToken);
            if (AutomationMarkRules.IsReal(query.Scope))
            {
                page.Logs = AutomationMarkRules.DropOwned(page.Logs, row => row.FacilityId, ownership, out var hidAny);
                page.ScopeNote = AutomationMarkRules.WithHiddenNote(page.ScopeNote, hidAny);
            }
        }

        foreach (var row in page.Logs)
            row.AutomationRunId = ownership.RunIdFor(row.FacilityId);
        return page;
    }

    private static SftpLogListPage EmptySftp(SftpQuery query, string note) => new()
    {
        Query = query,
        Search = new SftpSearch { PageSize = LogsRules.ClampPageSize(query.PageSize) },
        ScopeNote = note,
        Paging = new PageBar { Page = 1, PageSize = LogsRules.ClampPageSize(query.PageSize) }
    };

    private async Task<AuditListPage> AuditPage(AuditQuery query, CancellationToken cancellationToken)
    {
        query ??= new AuditQuery();
        if (!_features.Value.AutomationEnabled)
        {
            query.Scope = null;
            return await _logs.LoadAuditAsync(query, cancellationToken);
        }

        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var ownership = await _ownership.GetAsync(cancellationToken);
        var facility = string.IsNullOrWhiteSpace(query.FacilityId) ? null : query.FacilityId.Trim();
        AuditListPage page;
        if (AutomationMarkRules.IsAutomation(query.Scope))
        {
            if (facility is not null && !ownership.Contains(facility))
                page = EmptyAudit(query, AutomationMarkRules.NotOwnedNote);
            else if (facility is null)
            {
                var ids = ownership.NewestFacilityIds(AutomationMarkRules.MaxFacilitySearches, out var truncated);
                page = await _logs.LoadAuditForFacilitiesAsync(query, ids, truncated, cancellationToken);
            }
            else
                page = await _logs.LoadAuditAsync(query, cancellationToken);
        }
        else if (facility is not null && ownership.Contains(facility) && AutomationMarkRules.IsReal(query.Scope))
        {
            page = EmptyAudit(query, AutomationMarkRules.OwnedFacilityNote);
        }
        else
        {
            page = await _logs.LoadAuditAsync(query, cancellationToken);
            if (AutomationMarkRules.IsReal(query.Scope))
            {
                page.Events = AutomationMarkRules.DropOwned(page.Events, row => row.FacilityId, ownership, out var hidAny);
                page.ScopeNote = AutomationMarkRules.WithHiddenNote(page.ScopeNote, hidAny);
            }
        }

        foreach (var row in page.Events)
            row.AutomationRunId = ownership.RunIdFor(row.FacilityId);
        return page;
    }

    private static AuditListPage EmptyAudit(AuditQuery query, string note) => new()
    {
        Query = query,
        Search = new AuditSearch { PageSize = LogsRules.ClampAuditPageSize(query.PageSize) },
        ScopeNote = note,
        Paging = new PageBar { Page = 1, PageSize = LogsRules.ClampAuditPageSize(query.PageSize) }
    };

    private static AcquisitionLogListPage NotOwnedAcquisition(AcquisitionQuery query)
    {
        var pageSize = LogsRules.ClampPageSize(query.PageSize);
        return new AcquisitionLogListPage
        {
            Query = query,
            Search = new AcquisitionSearch
            {
                PageSize = pageSize,
                SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "ExecutionDate" : query.SortBy,
                SortDir = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc",
                MinAgeHours = query.MinAgeHours < 0 ? LogsRules.DefaultMinAgeHours : query.MinAgeHours
            },
            ScopeNote = AutomationMarkRules.NotOwnedNote,
            Paging = new PageBar { Page = 1, PageSize = pageSize }
        };
    }

    private IActionResult Back(AcquisitionQuery query, LogsAction result)
    {
        Temp(result);
        var route = new RouteValueDictionary();
        foreach (var pair in query.ToRoute())
            route[pair.Key] = pair.Value;
        return RedirectToAction(nameof(Acquisition), route);
    }

    private void Temp(LogsAction result)
    {
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;
    }
}
