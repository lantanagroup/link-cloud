using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class ReportServiceClient : LinkApiClientBase
{
    public ReportServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<(HttpStatusCode StatusCode, GenerateAdhocReportResponseApiModel? Response)> GenerateAdhocReportAsync(
        string facilityId,
        AdHocReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"facility/{facilityId}/AdhocReport")
            .AllowAnyHttpStatus()
            .PostJsonAsync(request, cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<GenerateAdhocReportResponseApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, ReportScheduleApiModel? Response)> GetScheduleAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"/schedules/{reportId}")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<ReportScheduleApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, byte[]? Bytes, string? ContentType, string? Body)> DownloadSubmissionAsync(
        string facilityId,
        string reportId,
        bool external = true,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"submission/{facilityId}/{reportId}")
            .SetQueryParam("external", external.ToString().ToLowerInvariant())
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        byte[]? bytes = null;
        string? body = null;
        if (response.ResponseMessage.IsSuccessStatusCode)
        {
            bytes = await response.GetBytesAsync();
        }
        else
        {
            body = await response.GetStringAsync();
        }

        return (response.ResponseMessage.StatusCode, bytes, response.ResponseMessage.Content.Headers.ContentType?.MediaType, body);
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<ReportScheduleApiModel>? Response)> SearchSchedulesAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var response = await Request("/schedules/search")
            .SetQueryParam("id", reportId)
            .SetQueryParam("pageSize", 10)
            .SetQueryParam("pageNumber", 1)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<ReportScheduleApiModel>>(response));
    }

    public async Task<(HttpStatusCode StatusCode, List<ReportEntryApiModel>? Response)> GetEntriesByScheduleAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"/entries/schedules/{reportId}")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        var status = response.ResponseMessage.StatusCode;

        if (status == HttpStatusCode.NotFound)
            return (status, []);

        if (status != HttpStatusCode.OK)
            return (status, null);

        return (status, await ReadListOrPagedRecordsSafeAsync<ReportEntryApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<ReportResourceApiModel>? Response)> SearchResourcesAsync(string facilityId, string reportId, int pageSize = 5000, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var response = await Request("/resources/search")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportScheduleId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<ReportResourceApiModel>>(response));
    }

    public async Task<(HttpStatusCode StatusCode, List<ReportPopulationApiModel>? Response)> GetPopulationsByScheduleAsync(string reportId, string? reportType = null, CancellationToken cancellationToken = default)
    {
        var request = Request($"/populations/schedules/{reportId}").AllowAnyHttpStatus();
        if (!string.IsNullOrWhiteSpace(reportType))
            request = request.SetQueryParam("reportType", reportType);

        var response = await request.GetAsync(cancellationToken: cancellationToken);
        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<List<ReportPopulationApiModel>>(response));
    }
}
