using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IFacilityServiceClient
{
    Task<LinkApiResponse<FacilityModel>> CreateAsync(FacilityModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityModel>> GetAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityModel>> UpdateAsync(string facilityId, FacilityModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<VendorModel>> CreateVendorAsync(CreateVendorModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<VendorModel>> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<VendorModel>>> GetVendorsAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse<VendorModel>> UpdateVendorAsync(Guid vendorId, UpdateVendorModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<VendorVersionModel>> CreateVendorVersionAsync(CreateVendorVersionModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<VendorVersionModel>> GetVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<VendorVersionModel>>> GetVendorVersionsAsync(Guid? vendorId = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<VendorVersionModel>> UpdateVendorVersionAsync(Guid vendorVersionId, UpdateVendorVersionModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CheckFacilityExistsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> RestoreAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SearchFacilitiesAsync(string? facilityId = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<Dictionary<string, string>>> GetFacilityListAsync(string? search = null, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>Facility page rows, including <see cref="FacilitySummary.IsTest"/>.</summary>
    Task<LinkApiResponse<List<FacilitySummary>>> GetFacilitySummariesAsync(string? search = null, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>Test flag for the facility ids on one page.</summary>
    Task<LinkApiResponse<List<FacilityFlag>>> GetFacilityFlagsAsync(FacilityFlagRequest request, CancellationToken cancellationToken = default);

    /// <summary>Paged facilities. <paramref name="isTest"/> limits the page to test or real facilities.</summary>
    Task<LinkApiResponse<PagedConfigModel<FacilityModel>>> SearchFacilitiesPageAsync(
        bool? isTest = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>Facility totals, and how many of an optional id set exist: <c>POST /api/Facility/counts</c>.</summary>
    Task<LinkApiResponse<FacilityCounts>> GetFacilityCountsAsync(FacilityCountRequest request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<GenerateAdhocReportResponseApiModel>> GenerateAdhocReportAsync(string facilityId, AdHocReportRequest request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<GenerateAdhocReportResponseApiModel>> RegenerateReportAsync(string facilityId, RegenerateReportRequest request, CancellationToken cancellationToken = default);
}
