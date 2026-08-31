using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IFacilityGateway over LinkSdk's IFacilityServiceClient. Reference adapter: responses go
// through LinkResponseHandler so no caller sees LinkApiResponse, and the write is a
// read-modify-write of a complete object.
internal sealed class FacilityGateway : IFacilityGateway
{
    private const string ServiceName = "Tenant";

    private readonly IFacilityServiceClient _facilityClient;
    private readonly ILogger<FacilityGateway> _logger;

    public FacilityGateway(IFacilityServiceClient facilityClient, ILogger<FacilityGateway> logger)
    {
        _facilityClient = facilityClient;
        _logger = logger;
    }

    public async Task<FacilityInfo?> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var current = await FetchAsync(facilityId, cancellationToken);
        return current is null ? null : FacilityInfoMapper.ToDomain(current);
    }

    public async Task SaveAsync(FacilityInfo facilityInfo, CancellationToken cancellationToken = default)
    {
        var current = await FetchAsync(facilityInfo.FacilityId, cancellationToken);
        var vendor = await ResolveVendorAsync(facilityInfo.Vendor, cancellationToken);

        if (current is null)
        {
            var created = FacilityInfoMapper.ForCreate(facilityInfo, vendor);
            var createResponse = await _facilityClient.CreateAsync(created, cancellationToken);
            LinkResponseHandler.Require(createResponse, ServiceName, nameof(SaveAsync));

            _logger.LogInformation("Created Tenant facility {FacilityId}.", facilityInfo.FacilityId);
            return;
        }

        // The fetched instance is handed straight to the mapper and never reconstructed, so
        // scheduledReports (and anything else Tenant owns) survives the write untouched.
        var updated = FacilityInfoMapper.Overlay(current, facilityInfo, vendor);
        var updateResponse = await _facilityClient.UpdateAsync(facilityInfo.FacilityId, updated, cancellationToken);
        LinkResponseHandler.Require(updateResponse, ServiceName, nameof(SaveAsync));

        _logger.LogInformation("Updated Tenant facility {FacilityId}.", facilityInfo.FacilityId);
    }

    private async Task<FacilityModel?> FetchAsync(string facilityId, CancellationToken cancellationToken)
    {
        var response = await _facilityClient.GetAsync(facilityId, cancellationToken);
        return LinkResponseHandler.Optional(response, ServiceName, nameof(GetAsync));
    }

    // Resolves our EhrVendor to the VendorModel Tenant holds, so a write carries the vendor's id
    // rather than a bare name.
    private async Task<VendorModel?> ResolveVendorAsync(EhrVendor? vendor, CancellationToken cancellationToken)
    {
        if (vendor is null)
        {
            return null;
        }

        var response = await _facilityClient.GetVendorsAsync(cancellationToken);
        var vendors = LinkResponseHandler.Require(response, ServiceName, nameof(ResolveVendorAsync));

        var match = vendors.FirstOrDefault(x => string.Equals(x.Name, vendor.Value.ToString(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            _logger.LogWarning("Tenant has no vendor named {Vendor}; the facility write will leave the vendor unchanged.", vendor);
        }

        return match;
    }
}
