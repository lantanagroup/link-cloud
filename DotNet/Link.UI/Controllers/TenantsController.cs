using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
    private readonly IOptions<LinkUiFeatureOptions> _features;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        IFacilityServiceClient facilityServiceClient,
        FacilityHubService hub,
        FacilityViewService view,
        ConfigurationService configuration,
        IOptions<LinkUiFeatureOptions> features,
        ILogger<TenantsController> logger)
    {
        _facilityServiceClient = facilityServiceClient;
        _hub = hub;
        _view = view;
        _configuration = configuration;
        _features = features;
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
        var automationOn = _features.Value.AutomationEnabled;
        var normalized = automationOn ? AutomationMarkRules.NormalizeScope(scope) : AutomationMarkRules.Real;

        try
        {
            var response = await _facilityServiceClient.GetFacilitySummariesAsync(
                search: search,
                includeDeleted: includeDeleted,
                cancellationToken: cancellationToken);
            if (!TryMapSummaries(response, out var summaries))
            {
                _logger.LogWarning(
                    "Facility list failed with status {StatusCode}. RequestUrl={RequestUrl} TraceId={TraceId}",
                    response.StatusCode,
                    response.RequestUrl,
                    response.TraceId);
                return View(FacilityListError(search, includeDeleted, normalized, response.StatusCode));
            }

            await MergeExactIdAsync(search, includeDeleted, summaries, cancellationToken);

            var tenants = summaries
                .Select(item => new TenantListItem
                {
                    FacilityId = item.FacilityId,
                    DisplayName = string.IsNullOrWhiteSpace(item.FacilityName) ? item.FacilityId : item.FacilityName,
                    IsDeleted = item.IsDeleted,
                    IsTest = item.IsTest
                })
                .Where(item => includeDeleted || !item.IsDeleted)
                .Where(item => !automationOn || AutomationMarkRules.Visible(normalized, item.IsTest))
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
            scope = _features.Value.AutomationEnabled ? AutomationMarkRules.ScopeForQuery(scope) : null
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
        return await FromResult(result, "New facility");
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
        RestoreNormalizationTempData(page);
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
        return await FromResult(result, input.FacilityName ?? id ?? "Facility");
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
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCensus([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteCensusAsync(id, cancellationToken);
        return await FromResult(result, id ?? "Facility");
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
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQueryDispatch([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteQueryDispatchAsync(id, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFhirQuery([FromRoute] string? id, FhirQueryPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveFhirQueryAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFhirQuery([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteFhirQueryAsync(id, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFhirList([FromRoute] string? id, FhirListPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveFhirListAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFhirList([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteFhirListAsync(id, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQueryPlan([FromRoute] string? id, QueryPlanPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveQueryPlanAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQueryPlan([FromRoute] string? id, string? type, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteQueryPlanAsync(id, type, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReportingOrg([FromRoute] string? id, ReportingOrgPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveReportingOrgAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReportingOrg([FromRoute] string? id, int? configId, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteReportingOrgAsync(id, configId, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSftp([FromRoute] string? id, SftpPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.SaveSftpAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSftp([FromRoute] string? id, string? configurationId, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteSftpAsync(id, configurationId, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSftpCredentials([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteSftpCredentialsAsync(id, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSavedSftp([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.TestSavedSftpAsync(id, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSftp([FromRoute] string? id, SftpPanel input, CancellationToken cancellationToken)
    {
        var result = await _hub.TestSftpAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOperation(
        [FromRoute] string? id,
        NormalizationOperationInput input,
        CancellationToken cancellationToken)
    {
        var result = await _hub.SaveOperationAsync(id, input, cancellationToken);
        return await FromResult(result, id ?? "Facility");
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
        return RedirectNormalizationStay(result, operationId: null)
            ?? await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperation(
        [FromRoute] string? id,
        string? operationId,
        CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteOperationAsync(id, operationId.Sanitize(), cancellationToken);
        return await FromResult(result, id ?? "Facility");
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
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperationSequence(
        [FromRoute] string? id,
        string? resourceType,
        CancellationToken cancellationToken)
    {
        var result = await _hub.DeleteOperationSequenceAsync(id, resourceType.Sanitize(), cancellationToken);
        return await FromResult(result, id ?? "Facility");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestOperation(
        [FromRoute] string? id,
        string? operationId,
        string? testResource,
        CancellationToken cancellationToken)
    {
        var operation = operationId.Sanitize();
        var result = await _hub.TestOperationAsync(id, operation, testResource, cancellationToken);
        return RedirectNormalizationStay(result, operation)
            ?? await FromResult(result, id ?? "Facility");
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
        return await FromResult(result, id ?? "Facility");
    }

    private async Task MergeExactIdAsync(
        string? search,
        bool includeDeleted,
        List<FacilitySummary> rows,
        CancellationToken cancellationToken)
    {
        if (!TenantListSearch.ShouldLookupExactId(search, rows.Select(row => row.FacilityId)))
            return;

        var term = search!.Trim();
        var full = await _facilityServiceClient.GetFacilitySummariesAsync(
            search: null,
            includeDeleted: includeDeleted,
            cancellationToken: cancellationToken);
        if (!TryMapSummaries(full, out var all))
            return;

        var match = all.FirstOrDefault(row => string.Equals(row.FacilityId, term, StringComparison.OrdinalIgnoreCase));
        if (match is null || (!includeDeleted && match.IsDeleted))
            return;

        rows.Add(match);
    }

    private static bool TryMapSummaries(
        LinkApiResponse<List<FacilitySummary>> response,
        out List<FacilitySummary> facilities)
    {
        if (response.StatusCode == StatusCodes.Status204NoContent
            || (response.IsSuccessStatusCode && response.Body is { Count: 0 }))
        {
            facilities = [];
            return true;
        }

        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            facilities = [];
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

    private IActionResult? RedirectNormalizationStay(FacilityWriteResult result, string? operationId)
    {
        if (result.RedirectToList || result.RedirectFacilityId is not null || result.Page is null)
            return null;

        var page = result.Page;
        if (page.NotFound || string.IsNullOrWhiteSpace(page.FacilityId))
            return null;

        var panel = page.Normalization;
        var hasResult = !string.IsNullOrWhiteSpace(panel.TestResult);
        var hasError = !string.IsNullOrWhiteSpace(page.NormalizationError);
        if (!hasResult && !hasError)
            return null;

        if (hasResult)
        {
            TempData["NormalizationTestResult"] = panel.TestResult;
            TempData["NormalizationTestFailed"] = panel.TestFailed ? "1" : "0";
        }

        var posted = panel.Editor.TestResource;
        if (!string.IsNullOrEmpty(posted) && posted.Length <= 4000)
            TempData["NormalizationTestResource"] = posted;

        if (hasError)
            TempData["NormalizationError"] = page.NormalizationError;

        var openId = panel.EditorOpen
            ? (string.IsNullOrWhiteSpace(operationId) ? panel.Editor.OperationId : operationId)
            : null;
        return RedirectToAction(nameof(Facility), new { id = page.FacilityId, operationId = openId });
    }

    private void RestoreNormalizationTempData(FacilityHubViewModel page)
    {
        if (TempData["NormalizationError"] is string error && string.IsNullOrWhiteSpace(page.NormalizationError))
            page.NormalizationError = error;

        if (TempData["NormalizationTestResult"] is string testResult)
        {
            page.Normalization.TestResult = testResult;
            page.Normalization.TestFailed = string.Equals(
                TempData["NormalizationTestFailed"] as string,
                "1",
                StringComparison.Ordinal);
        }

        if (TempData["NormalizationTestResource"] is string resource
            && page.Normalization.EditorOpen
            && string.IsNullOrEmpty(page.Normalization.Editor.TestResource))
            page.Normalization.Editor.TestResource = resource;
    }

    private Task<IActionResult> FromResult(FacilityWriteResult result, string title)
    {
        if (result.RedirectToList)
        {
            TempData["Message"] = result.RedirectMessage;
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index)));
        }

        if (result.RedirectFacilityId is not null)
        {
            TempData["Message"] = result.RedirectMessage;
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Facility), new
            {
                id = result.RedirectFacilityId,
                planType = result.RedirectPlanType,
                orgConfig = result.RedirectReportingOrgId,
                sequenceType = result.RedirectSequenceType,
                operationPage = result.RedirectOperationPage
            }));
        }

        ViewData["Title"] = title;
        return Task.FromResult<IActionResult>(View("Facility", result.Page));
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
