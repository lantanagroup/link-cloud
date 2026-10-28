using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IQueryDispatchServiceClient
{
    Task<LinkApiResponse<QueryDispatchConfigurationApiModel>> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateQueryDispatchConfigurationAsync(QueryDispatchConfigurationApiModel configuration, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpsertQueryDispatchConfigurationAsync(string facilityId, QueryDispatchConfigurationApiModel configuration, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteQueryDispatchConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
}
