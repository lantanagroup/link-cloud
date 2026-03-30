using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class ReportServiceClient : LinkApiClientBase
{
    public ReportServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task<GenerateAdhocReportResponseApiModel> GenerateAdhocReportAsync(
        string facilityId,
        AdHocReportRequest request,
        CancellationToken cancellationToken = default) =>
        Request($"facility/{facilityId}/AdhocReport")
            .PostJsonAsync(request, cancellationToken: cancellationToken)
            .ReceiveJson<GenerateAdhocReportResponseApiModel>();

    public Task<ReportScheduleApiModel> GetScheduleAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        Request($"/schedules/{reportId}")
            .GetJsonAsync<ReportScheduleApiModel>(cancellationToken: cancellationToken);

    public async Task<(byte[] Bytes, string? ContentType)> DownloadSubmissionAsync(
        string facilityId,
        string reportId,
        bool external = true,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"submission/{facilityId}/{reportId}")
            .SetQueryParam("external", external.ToString().ToLowerInvariant())
            .GetAsync(cancellationToken: cancellationToken);

        return (await response.GetBytesAsync(), response.ResponseMessage.Content.Headers.ContentType?.MediaType);
    }

    public Task<PagedConfigModel<ReportScheduleApiModel>> SearchSchedulesAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        Request("/schedules/search")
            .SetQueryParam("id", reportId)
            .SetQueryParam("pageSize", 10)
            .SetQueryParam("pageNumber", 1)
            .GetJsonAsync<PagedConfigModel<ReportScheduleApiModel>>(cancellationToken: cancellationToken);

    public Task<List<ReportEntryApiModel>> GetEntriesByScheduleAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        Request($"/entries/schedules/{reportId}")
            .GetJsonAsync<List<ReportEntryApiModel>>(cancellationToken: cancellationToken);

    public Task<PagedConfigModel<ReportResourceApiModel>> SearchResourcesAsync(
        string facilityId,
        string reportId,
        int pageSize = 5000,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        Request("/resources/search")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportScheduleId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetJsonAsync<PagedConfigModel<ReportResourceApiModel>>(cancellationToken: cancellationToken);

    public Task<List<ReportPopulationApiModel>> GetPopulationsByScheduleAsync(
        string reportId,
        string? reportType = null,
        CancellationToken cancellationToken = default)
    {
        var request = Request($"/populations/schedules/{reportId}");
        if (!string.IsNullOrWhiteSpace(reportType))
            request = request.SetQueryParam("reportType", reportType);

        return request.GetJsonAsync<List<ReportPopulationApiModel>>(cancellationToken: cancellationToken);
    }
}
