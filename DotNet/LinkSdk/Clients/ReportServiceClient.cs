using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class ReportServiceClient : LinkApiClientBase, IReportServiceClient
{
    public ReportServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.ReportServiceApiUrl
                ?? throw new InvalidOperationException("Report service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<GenerateAdhocReportResponseApiModel> GenerateAdhocReportAsync(string facilityId, AdHocReportRequest request, CancellationToken cancellationToken = default) =>
        Request($"facility/{facilityId}/AdhocReport").PostJsonAsync(request, cancellationToken: cancellationToken).ReceiveJson<GenerateAdhocReportResponseApiModel>();

    public Task<ReportScheduleApiModel?> GetScheduleAsync(string reportId, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"/schedules/{reportId}").GetJsonAsync<ReportScheduleApiModel>(cancellationToken: cancellationToken));

    public async Task<(byte[] Bytes, string? ContentType)> DownloadSubmissionAsync(string facilityId, string reportId, bool external = true, CancellationToken cancellationToken = default)
    {
        var response = await Request($"submission/{facilityId}/{reportId}").SetQueryParam("external", external.ToString().ToLowerInvariant()).GetAsync(cancellationToken: cancellationToken);
        return (await response.GetBytesAsync(), response.ResponseMessage.Content.Headers.ContentType?.MediaType);
    }

    public Task<PagedConfigModel<ReportScheduleApiModel>> SearchSchedulesAsync(string reportId, CancellationToken cancellationToken = default) =>
        Request("/schedules/search").SetQueryParam("id", reportId).SetQueryParam("pageSize", 10).SetQueryParam("pageNumber", 1).GetJsonAsync<PagedConfigModel<ReportScheduleApiModel>>(cancellationToken: cancellationToken);

    public async Task<List<ReportEntryApiModel>> GetEntriesByScheduleAsync(string reportId, CancellationToken cancellationToken = default) =>
        await GetOrDefaultAsync(() => Request($"/entries/schedules/{reportId}").GetJsonAsync<List<ReportEntryApiModel>>(cancellationToken: cancellationToken)) ?? [];

    public Task<PagedConfigModel<ReportResourceApiModel>> SearchResourcesAsync(string facilityId, string reportId, int pageSize = 5000, int pageNumber = 1, CancellationToken cancellationToken = default) =>
        Request("/resources/search").SetQueryParam("facilityId", facilityId).SetQueryParam("reportScheduleId", reportId).SetQueryParam("pageSize", pageSize).SetQueryParam("pageNumber", pageNumber).GetJsonAsync<PagedConfigModel<ReportResourceApiModel>>(cancellationToken: cancellationToken);

    public async Task<List<ReportPopulationApiModel>> GetPopulationsByScheduleAsync(string reportId, string? reportType = null, CancellationToken cancellationToken = default)
    {
        var r = Request($"/populations/schedules/{reportId}");
        if (!string.IsNullOrWhiteSpace(reportType)) r = r.SetQueryParam("reportType", reportType);
        return await GetOrDefaultAsync(() => r.GetJsonAsync<List<ReportPopulationApiModel>>(cancellationToken: cancellationToken)) ?? [];
    }
}
