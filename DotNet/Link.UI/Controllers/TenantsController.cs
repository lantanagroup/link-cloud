using LantanaGroup.Link.Sdk.Clients;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Tenant list and the facility hub (identity, census, query dispatch, data acquisition) via LinkSDK.
/// </summary>
public sealed class TenantsController : Controller
{
    private readonly IFacilityServiceClient _facilityServiceClient;
    private readonly FacilityHubService _hub;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        IFacilityServiceClient facilityServiceClient,
        FacilityHubService hub,
        ILogger<TenantsController> logger)
    {
        _facilityServiceClient = facilityServiceClient;
        _hub = hub;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Tenants";

        try
        {
            var response = await _facilityServiceClient.GetFacilityListAsync(
                search: search,
                includeDeleted: false,
                cancellationToken: cancellationToken);

            // Tenant returns 204 when the facility list is empty.
            if (response.StatusCode == StatusCodes.Status204NoContent
                || (response.IsSuccessStatusCode && response.Body is { Count: 0 }))
            {
                return View(new TenantListViewModel
                {
                    Search = search,
                    LoadedSuccessfully = true
                });
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                _logger.LogWarning(
                    "Facility list failed with status {StatusCode}. RequestUrl={RequestUrl} TraceId={TraceId}",
                    response.StatusCode,
                    response.RequestUrl,
                    response.TraceId);

                var detail = response.StatusCode == 0
                    ? "Tenant service could not be reached."
                    : $"Tenant service returned HTTP {response.StatusCode}.";

                return View(new TenantListViewModel
                {
                    Search = search,
                    LoadedSuccessfully = false,
                    ErrorMessage = $"Unable to load tenants. {detail} " +
                                   "Confirm ServiceRegistry:TenantService:TenantServiceUrl and Link token settings."
                });
            }

            var tenants = response.Body
                .OrderBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new TenantListItem
                {
                    FacilityId = kvp.Key,
                    DisplayName = string.IsNullOrWhiteSpace(kvp.Value) ? kvp.Key : kvp.Value
                })
                .ToList();

            return View(new TenantListViewModel
            {
                Search = search,
                Tenants = tenants,
                LoadedSuccessfully = true
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
                LoadedSuccessfully = false,
                ErrorMessage = "Tenant service call failed: " + ex.Message
            });
        }
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
    public async Task<IActionResult> Facility([FromRoute] string? id, string? planType, int? orgConfig, CancellationToken cancellationToken)
    {
        var page = await _hub.LoadEditAsync(id, planType, orgConfig, cancellationToken);
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
    public async Task<IActionResult> Remove([FromRoute] string? id, CancellationToken cancellationToken)
    {
        var result = await _hub.SoftDeleteAsync(id, cancellationToken);
        return FromResult(result, id ?? "Facility");
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
                orgConfig = result.RedirectReportingOrgId
            });
        }

        ViewData["Title"] = title;
        return View("Facility", result.Page);
    }
}
