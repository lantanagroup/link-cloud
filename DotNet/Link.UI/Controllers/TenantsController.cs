using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Tenant list and the facility hub (identity, census, query dispatch, data acquisition, normalization) via LinkSDK.
/// </summary>
public sealed class TenantsController : Controller
{
    private readonly IFacilityServiceClient _facilityServiceClient;
    private readonly FacilityHubService _hub;
    private readonly FacilityViewService _view;
    private readonly ConfigurationService _configuration;
    private readonly AutomationOwnershipLookup _ownership;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        IFacilityServiceClient facilityServiceClient,
        FacilityHubService hub,
        FacilityViewService view,
        ConfigurationService configuration,
        AutomationOwnershipLookup ownership,
        ILogger<TenantsController> logger)
    {
        _facilityServiceClient = facilityServiceClient;
        _hub = hub;
        _view = view;
        _configuration = configuration;
        _ownership = ownership;
        _logger = logger;
    }

    public async Task<IActionResult> Index(
        string? search,
        bool includeDeleted,
        string? scope,
        int page = 1,
        int pageSize = 0,
        CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Tenants";
        var normalized = AutomationMarkRules.NormalizeScope(scope);
        var ownership = await _ownership.GetAsync(cancellationToken);

        try
        {
            var activeResponse = await _facilityServiceClient.GetFacilityListAsync(
                search: search,
                includeDeleted: false,
                cancellationToken: cancellationToken);
            if (!TryMapFacilities(activeResponse, out var active))
            {
                _logger.LogWarning(
                    "Facility list failed with status {StatusCode}. RequestUrl={RequestUrl} TraceId={TraceId}",
                    activeResponse.StatusCode,
                    activeResponse.RequestUrl,
                    activeResponse.TraceId);
                return View(FacilityListError(search, includeDeleted, normalized, activeResponse.StatusCode));
            }

            string? deletedNote = null;
            Dictionary<string, string> all = active;
            if (includeDeleted)
            {
                var allResponse = await _facilityServiceClient.GetFacilityListAsync(
                    search: search,
                    includeDeleted: true,
                    cancellationToken: cancellationToken);
                if (!TryMapFacilities(allResponse, out all))
                {
                    all = active;
                    deletedNote = "Deleted facilities could not be loaded.";
                    _logger.LogWarning(
                        "Deleted facility list failed with status {StatusCode}. RequestUrl={RequestUrl} TraceId={TraceId}",
                        allResponse.StatusCode,
                        allResponse.RequestUrl,
                        allResponse.TraceId);
                }
            }

            var activeKeys = new HashSet<string>(active.Keys, StringComparer.OrdinalIgnoreCase);
            var tenants = all
                .Select(kvp => new TenantListItem
                {
                    FacilityId = kvp.Key,
                    DisplayName = string.IsNullOrWhiteSpace(kvp.Value) ? kvp.Key : kvp.Value,
                    IsDeleted = !activeKeys.Contains(kvp.Key),
                    AutomationRunId = ownership.RunIdFor(kvp.Key)
                })
                .Where(item => includeDeleted || !item.IsDeleted)
                .Where(item => !AutomationMarkRules.IsAutomation(normalized) || item.AutomationRunId is not null)
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FacilityId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var slice = TenantListRules.Slice(tenants, page, pageSize);

            return View(new TenantListViewModel
            {
                Search = search,
                Scope = normalized,
                IncludeDeleted = includeDeleted,
                Tenants = slice.Items,
                LoadedSuccessfully = true,
                DeletedNote = deletedNote,
                Page = slice.PageNumber,
                PageSize = slice.PageSize,
                TotalCount = slice.TotalCount,
                TotalPages = slice.TotalPages
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facility list call threw");
            return View(new TenantListViewModel
            {
                Search = search,
                Scope = normalized,
                IncludeDeleted = includeDeleted,
                LoadedSuccessfully = false,
                ErrorMessage = "Tenant service call failed: " + ex.Message
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(
        string? id,
        string? search,
        bool includeDeleted,
        string? scope,
        int page = 1,
        int pageSize = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await _view.RestoreFacilityAsync(id, cancellationToken);
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index), new
        {
            search,
            includeDeleted,
            page,
            pageSize,
            scope = AutomationMarkRules.IsAutomation(scope) ? AutomationMarkRules.Automation : null
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New facility";
        return View("Facility", await _hub.LoadCreateAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FacilityEditInput input, CancellationToken cancellationToken)
    {
        var result = await _hub.CreateAsync(input, cancellationToken);
        return FromResult(result, "New facility");
    }

    [HttpGet]
    public async Task<IActionResult> Facility(
        [FromRoute] string? id,
        string? planType,
        string? planEdit,
        int? orgConfig,
        string? operationId,
        string? operationType,
        string? sequenceType,
        int? operationPage,
        CancellationToken cancellationToken)
    {
        var page = await _hub.LoadEditAsync(
            id,
            planType.Sanitize(),
            orgConfig,
            cancellationToken,
            operationId.Sanitize(),
            operationType.Sanitize(),
            sequenceType.Sanitize(),
            operationPage ?? 1);
        if (!page.IsCreate && !page.NotFound && string.IsNullOrWhiteSpace(page.LoadError) && !string.IsNullOrWhiteSpace(page.FacilityId))
            page.Notification = await _configuration.LoadFacilityNotificationAsync(page.FacilityId, cancellationToken);
        var editor = planEdit.Sanitize();
        if (string.Equals(editor, "add", StringComparison.OrdinalIgnoreCase)
            || string.Equals(editor, "edit", StringComparison.OrdinalIgnoreCase))
            page.QueryPlanEditor = editor.ToLowerInvariant();
        ViewData["Title"] = page.FacilityName ?? page.FacilityId ?? "Facility";
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFacility([FromRoute] string? id, FacilityEditInput input, CancellationToken cancellationToken)
    {
        var result = await _hub.UpdateAsync(id, input, cancellationToken);
        return FromResult(result, input.FacilityName ?? id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCensus(
        [FromRoute] string? id,
        bool enabled,
        string? scheduledTrigger,
        bool censusExists,
        CancellationToken cancellationToken)
    {
        var result = await _hub.SaveCensusAsync(id, enabled, scheduledTrigger, censusExists, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCensus([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteCensusAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQueryDispatch(
        [FromRoute] string? id,
        List<DispatchScheduleInput>? schedules,
        bool queryDispatchExists,
        CancellationToken cancellationToken)
    {
        var result = await _hub.SaveQueryDispatchAsync(id, schedules, queryDispatchExists, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQueryDispatch([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteQueryDispatchAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFhirQuery([FromRoute] string? id, FhirQueryPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveFhirQueryAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFhirQuery([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteFhirQueryAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFhirList([FromRoute] string? id, FhirListPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveFhirListAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFhirList([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteFhirListAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQueryPlan([FromRoute] string? id, QueryPlanPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveQueryPlanAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQueryPlan([FromRoute] string? id, string? type, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteQueryPlanAsync(id, type, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReportingOrg([FromRoute] string? id, ReportingOrgPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveReportingOrgAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReportingOrg([FromRoute] string? id, int? configId, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteReportingOrgAsync(id, configId, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSftp([FromRoute] string? id, SftpPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveSftpAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSftp([FromRoute] string? id, string? configurationId, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteSftpAsync(id, configurationId, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSftpCredentials([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteSftpCredentialsAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSavedSftp([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.TestSavedSftpAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSftp([FromRoute] string? id, SftpPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.TestSftpAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOperation(
        [FromRoute] string? id,
        NormalizationOperationInput input,
        CancellationToken cancellationToken)
    {
        var result = await _hub.SaveOperationAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(256 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 256 * 1024)]
    public async Task<IActionResult> ImportExtensionUrls(
        [FromRoute] string? id,
        IFormFile? csv,
        CancellationToken cancellationToken)
    {
        string? text = null;
        var tooLarge = csv is { Length: > FacilityNormalizationRules.MaxImportCharacters };
        if (csv is { Length: > 0 } && !tooLarge)
        {
            using var reader = new StreamReader(csv.OpenReadStream());
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        var result = await _hub.ImportExtensionUrlsAsync(id, text, tooLarge, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperation(
        [FromRoute] string? id,
        string? operationId,
        CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteOperationAsync(id, operationId.Sanitize(), cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOperationSequence(
        [FromRoute] string? id,
        NormalizationSequenceInput input,
        CancellationToken cancellationToken)
    {
        input.ResourceType = input.ResourceType.Sanitize();
        var result = await _hub.SaveOperationSequenceAsync(id, input, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperationSequence(
        [FromRoute] string? id,
        string? resourceType,
        CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteOperationSequenceAsync(id, resourceType.Sanitize(), cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestOperation(
        [FromRoute] string? id,
        string? operationId,
        string? testResource,
        CancellationToken cancellationToken)
    {
        var result = await _hub.TestOperationAsync(id, operationId.Sanitize(), testResource, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    [HttpGet]
    public async Task<IActionResult> View([FromRoute] string? id, FacilityViewQuery query, CancellationToken cancellationToken)
    {
        var page = await _view.LoadAsync(id, query, cancellationToken);
        ViewData["Title"] = page.FacilityName ?? page.FacilityId ?? "Facility";
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Report(
        [FromRoute] string? id,
        string? reportId,
        ReportPageQuery query,
        CancellationToken cancellationToken)
    {
        var page = await _view.LoadReportAsync(id, reportId, query, cancellationToken);
        ViewData["Title"] = page.ReportId.Length == 0 ? "Report" : page.ReportId;
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResubmitReport(
        [FromRoute] string? id,
        string? targetId,
        bool bypassSubmission,
        FacilityViewQuery query,
        CancellationToken cancellationToken) =>
        ReportAction(id, query, cancellationToken, () => _view.ResubmitAsync(id, targetId, bypassSubmission, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AbortReport(
        [FromRoute] string? id,
        string? targetId,
        FacilityViewQuery query,
        CancellationToken cancellationToken) =>
        ReportAction(id, query, cancellationToken, () => _view.AbortAsync(id, targetId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CleanUpReport(
        [FromRoute] string? id,
        string? targetId,
        FacilityViewQuery query,
        CancellationToken cancellationToken) =>
        ReportAction(id, query, cancellationToken, () => _view.CleanUpAsync(id, targetId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreReport(
        [FromRoute] string? id,
        string? targetId,
        FacilityViewQuery query,
        CancellationToken cancellationToken) =>
        ReportAction(id, query, cancellationToken, () => _view.RestoreReportAsync(id, targetId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.SoftDeleteAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
    }

    private static bool TryMapFacilities(
        LantanaGroup.Link.Sdk.ApiClient.LinkApiResponse<Dictionary<string, string>> response,
        out Dictionary<string, string> facilities)
    {
        // Tenant returns 204 when the facility list is empty.
        if (response.StatusCode == StatusCodes.Status204NoContent
            || (response.IsSuccessStatusCode && response.Body is { Count: 0 }))
        {
            facilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            facilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return false;
        }

        facilities = response.Body;
        return true;
    }

    private TenantListViewModel FacilityListError(string? search, bool includeDeleted, string scope, int statusCode)
    {
        var detail = statusCode == 0
            ? "Tenant service could not be reached."
            : $"Tenant service returned HTTP {statusCode}.";
        return new TenantListViewModel
        {
            Search = search,
            Scope = scope,
            IncludeDeleted = includeDeleted,
            LoadedSuccessfully = false,
            ErrorMessage = "Unable to load tenants. " + detail +
                           " Confirm ServiceRegistry:TenantService:TenantServiceUrl and Link token settings."
        };
    }

    private IActionResult FromResult(FacilityWriteResult result, string title)
    {
        if (result.RedirectToList)
        {
            TempData["Message"] = result.RedirectMessage;
            return RedirectToAction(nameof(Index));
        }

        if (result.RedirectFacilityId is not null)
        {
            TempData["Message"] = result.RedirectMessage;
            return RedirectToAction(nameof(Facility), new
            {
                id = result.RedirectFacilityId,
                planType = result.RedirectPlanType,
                orgConfig = result.RedirectReportingOrgId,
                sequenceType = result.RedirectSequenceType,
                operationPage = result.RedirectOperationPage
            });
        }

        ViewData["Title"] = title;
        return View("Facility", result.Page);
    }

    private async Task<IActionResult> ReportAction(
        string? id,
        FacilityViewQuery query,
        CancellationToken cancellationToken,
        Func<Task<FacilityViewAction>> action)
    {
        var result = await action();
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;

        var route = new RouteValueDictionary { ["id"] = id };
        foreach (var (key, value) in query.ToRoute())
            route[key] = value;
        return RedirectToAction(nameof(View), route);
    }
}
