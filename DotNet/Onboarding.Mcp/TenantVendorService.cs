using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record TenantVendorVersion(Guid Id, string Version);
public sealed record TenantVendor(Guid Id, string Name, TenantVendorVersion[] Versions);
public sealed record TenantVendorCatalogResult(string Status, TenantVendor[] Vendors, string Message);

public sealed class TenantVendorService(HttpClient httpClient, IOptions<OnboardingOptions> options)
{
    // These DTOs deliberately exclude authentication, which the Tenant API also returns.
    private sealed record VendorDto(Guid Id, string Name);
    private sealed record VersionDto(Guid Id, Guid VendorId, string Version);

    public async Task<TenantVendorCatalogResult> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var vendors = await ReadAsync<VendorDto>("Vendor", cancellationToken);
            var versions = await ReadAsync<VersionDto>("VendorVersion", cancellationToken);
            if (vendors.Any(v => v is null || v.Id == Guid.Empty || string.IsNullOrWhiteSpace(v.Name))
                || vendors.Select(v => v.Id).Distinct().Count() != vendors.Length
                || versions.Any(v => v is null || v.Id == Guid.Empty || string.IsNullOrWhiteSpace(v.Version)
                    || !vendors.Any(parent => parent.Id == v.VendorId))
                || versions.Select(v => v.Id).Distinct().Count() != versions.Length)
                throw new JsonException("Invalid vendor catalog.");

            var records = vendors.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .Select(v => new TenantVendor(v.Id, v.Name, versions.Where(version => version.VendorId == v.Id)
                    .OrderBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                    .Select(version => new TenantVendorVersion(version.Id, version.Version)).ToArray())).ToArray();
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

    private async Task<T[]> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.Value.TenantApiBaseUrl), path));
        if (!string.IsNullOrWhiteSpace(options.Value.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.BearerToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        // Older list endpoints can return 204 for an empty catalog.
        if (response.StatusCode == HttpStatusCode.NoContent) return [];
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken)
            ?? throw new JsonException("Missing vendor catalog.");
    }

    private static TenantVendorCatalogResult Unavailable() =>
        new("unavailable", [], "Tenant vendor catalog could not be retrieved. Check Tenant availability and permissions; no catalog was confirmed.");
}
