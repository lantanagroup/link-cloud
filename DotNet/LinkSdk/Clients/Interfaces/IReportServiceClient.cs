using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IReportServiceClient
{
    Task<GenerateAdhocReportResponseApiModel> GenerateAdhocReportAsync(string facilityId, AdHocReportRequest request, CancellationToken cancellationToken = default);
    Task<ReportScheduleApiModel?> GetScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string? ContentType)> DownloadSubmissionAsync(string facilityId, string reportId, bool external = true, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<ReportScheduleApiModel>> SearchSchedulesAsync(string reportId, CancellationToken cancellationToken = default);
    Task<List<ReportEntryApiModel>> GetEntriesByScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<ReportResourceApiModel>> SearchResourcesAsync(string facilityId, string reportId, int pageSize = 5000, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<List<ReportPopulationApiModel>> GetPopulationsByScheduleAsync(string reportId, string? reportType = null, CancellationToken cancellationToken = default);
}
