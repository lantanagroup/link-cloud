using Flurl.Http;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// Report's entry-detail route carries AggregateReportUri/MeasureReportUri, fields LinkSdk's typed
// model doesn't declare -- see IReportRawClient. Built on LinkApiClientBase, the same base LinkSdk's
// generated clients use, for base-URL resolution and system-token injection. Mirrors
// ValidationRawClient/NormalizationRawClient.
internal sealed class ReportRawClient : LinkApiClientBase, IReportRawClient
{
    private const string ServiceName = "Report";

    public ReportRawClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
        ICreateSystemToken createSystemToken)
        : base(
            serviceRegistry.Value.ReportServiceApiUrl
                ?? throw new InvalidOperationException("Report service URL is not configured in ServiceRegistry."),
            linkBearerServiceOptions, linkTokenServiceConfig, createSystemToken)
    { }

    public async Task<string?> GetEntryDetailRawAsync(string reportScheduleId, string patientId, CancellationToken cancellationToken = default)
    {
        var response = await SendStringAsync(() => Request($"entries/schedules/{reportScheduleId}/patients/{patientId}")
            .GetAsync(cancellationToken: cancellationToken));

        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new LinkServiceException(ServiceName, nameof(GetEntryDetailRawAsync), response.StatusCode,
                response.TraceId, response.RawBody, response.RequestUrl);
        }

        return response.Body ?? "";
    }
}
