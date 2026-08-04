using LantanaGroup.Link.Sdk.Clients;
using TenantVendorVersionModel = LantanaGroup.Link.Shared.Application.Models.Tenant.VendorVersionModel;

namespace LantanaGroup.Link.Normalization.Domain.Services;

public interface IVendorVersionResolver
{
    Task<IReadOnlyDictionary<Guid, TenantVendorVersionModel>> ResolveAsync(IEnumerable<Guid> vendorVersionIds, CancellationToken cancellationToken = default);
}

public sealed class VendorVersionResolver : IVendorVersionResolver
{
    private readonly IFacilityServiceClient _facilityServiceClient;

    public VendorVersionResolver(IFacilityServiceClient facilityServiceClient)
    {
        _facilityServiceClient = facilityServiceClient;
    }

    public async Task<IReadOnlyDictionary<Guid, TenantVendorVersionModel>> ResolveAsync(
        IEnumerable<Guid> vendorVersionIds,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = vendorVersionIds.Distinct().ToList();
        if (requestedIds.Count == 0)
        {
            return new Dictionary<Guid, TenantVendorVersionModel>();
        }

        var response = await _facilityServiceClient.GetVendorVersionsAsync(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || response.Body == null)
        {
            throw new InvalidOperationException($"Unable to retrieve vendor versions from Tenant. Tenant returned HTTP {response.StatusCode}.");
        }

        var requestedIdSet = requestedIds.ToHashSet();
        var resolvedVersions = response.Body
            .Where(vendorVersion => vendorVersion.Id.HasValue && requestedIdSet.Contains(vendorVersion.Id.Value))
            .GroupBy(vendorVersion => vendorVersion.Id!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var missingIds = requestedIds.Where(id => !resolvedVersions.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new InvalidOperationException($"Tenant does not contain vendor version ID(s): {string.Join(", ", missingIds)}.");
        }

        return resolvedVersions;
    }
}