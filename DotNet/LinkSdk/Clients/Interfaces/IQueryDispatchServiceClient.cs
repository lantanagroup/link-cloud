using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IQueryDispatchServiceClient
{
    Task UpsertQueryDispatchConfigurationAsync(string facilityId, QueryDispatchConfigurationApiModel configuration, CancellationToken cancellationToken = default);
    Task DeleteQueryDispatchConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
}
