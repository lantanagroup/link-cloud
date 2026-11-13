using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record TenantVendorVersion(Guid Id, string Version);
public sealed record TenantVendor(Guid Id, string Name, TenantVendorVersion[] Versions);
public sealed record TenantVendorCatalogResult(string Status, TenantVendor[] Vendors, string Message);

public sealed class TenantVendorService(IFacilityServiceClient facilityClient)
{
    public async Task<TenantVendorCatalogResult> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var vendors = Read(await facilityClient.GetVendorsAsync(cancellationToken), cancellationToken);
            var versions = Read(await facilityClient.GetVendorVersionsAsync(cancellationToken: cancellationToken), cancellationToken);
            if (vendors.Any(v => v is null || v.Id is null || v.Id == Guid.Empty || string.IsNullOrWhiteSpace(v.Name))
                || vendors.Select(v => v.Id).Distinct().Count() != vendors.Count
                || versions.Any(v => v is null || v.Id is null || v.Id == Guid.Empty || string.IsNullOrWhiteSpace(v.Version)
                    || !vendors.Any(parent => parent.Id == v.VendorId))
                || versions.Select(v => v.Id).Distinct().Count() != versions.Count)
                throw new JsonException("Invalid vendor catalog.");

            var records = vendors.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .Select(v => new TenantVendor(v.Id!.Value, v.Name!, versions.Where(version => version.VendorId == v.Id)
                    .OrderBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                    .Select(version => new TenantVendorVersion(version.Id!.Value, version.Version!)).ToArray())).ToArray();
            return new("available", records, "Vendor catalog from Tenant. Version IDs may be used for facility vendor associations.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            return Unavailable();
        }
    }

    private static List<T> Read<T>(LinkApiResponse<List<T>> response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Older list endpoints can return 204 for an empty catalog.
        if (response.StatusCode == (int)HttpStatusCode.NoContent) return [];
        if (!response.IsSuccessStatusCode || response.Body is null)
            throw new JsonException("Missing vendor catalog.");
        return response.Body;
    }

    private static TenantVendorCatalogResult Unavailable() =>
        new("unavailable", [], "Tenant vendor catalog could not be retrieved. Check Tenant availability and permissions; no catalog was confirmed.");
}
