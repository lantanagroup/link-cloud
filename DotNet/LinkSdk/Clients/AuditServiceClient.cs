using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Audit;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class AuditServiceClient : LinkApiClientBase, IAuditServiceClient
{
    public AuditServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.AuditServiceApiUrl
                ?? throw new InvalidOperationException("Audit service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<PagedAuditApiModel>> SearchAsync(
        string? searchText = null,
        string? facility = null,
        string? correlationId = null,
        string? service = null,
        string? action = null,
        string? user = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedAuditApiModel>(() =>
        {
            var request = Request("audit")
                .SetQueryParam("pageNumber", pageNumber)
                .SetQueryParam("pageSize", pageSize);
            request = Set(request, "searchText", searchText);
            request = Set(request, "facility", facility);
            request = Set(request, "correlationId", correlationId);
            request = Set(request, "service", service);
            request = Set(request, "action", action);
            request = Set(request, "user", user);
            request = Set(request, "sortBy", sortBy);
            request = Set(request, "sortOrder", sortOrder);
            return request.GetAsync(cancellationToken: cancellationToken);
        });

    public Task<LinkApiResponse<AuditEventApiModel>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync<AuditEventApiModel>(() => Request($"audit/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<AuditErrorCount>> GetErrorCountAsync(
        int hours,
        CancellationToken cancellationToken = default) =>
        SendAsync<AuditErrorCount>(() => Request("audit/errors")
            .SetQueryParam("hours", hours)
            .GetAsync(cancellationToken: cancellationToken));

    private static IFlurlRequest Set(IFlurlRequest request, string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? request : request.SetQueryParam(name, value);
}
