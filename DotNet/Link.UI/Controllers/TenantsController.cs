using LantanaGroup.Link.Sdk.Clients;
using Link.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Phase-1 proof page: read-only tenant/facility list via LinkSDK IFacilityServiceClient.
/// </summary>
public sealed class TenantsController : Controller
{
    private readonly IFacilityServiceClient _facilityServiceClient;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(IFacilityServiceClient facilityServiceClient, ILogger<TenantsController> logger)
    {
        _facilityServiceClient = facilityServiceClient;
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

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                _logger.LogWarning(
                    "Facility list failed with status {StatusCode}. RequestUrl={RequestUrl} TraceId={TraceId}",
                    response.StatusCode,
                    response.RequestUrl,
                    response.TraceId);

                return View(new TenantListViewModel
                {
                    Search = search,
                    LoadedSuccessfully = false,
                    ErrorMessage = $"Unable to load tenants from Tenant service (HTTP {response.StatusCode}). " +
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
}
