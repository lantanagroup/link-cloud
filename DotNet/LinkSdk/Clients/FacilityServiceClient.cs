using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class FacilityServiceClient : LinkApiClientBase
{
    public FacilityServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task<FacilityModel> CreateAsync(
        FacilityModel request,
        CancellationToken cancellationToken = default) =>
        Request("/Facility")
            .PostJsonAsync(request, cancellationToken: cancellationToken)
            .ReceiveJson<FacilityModel>();

    public Task<FacilityModel> GetAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"/Facility/{facilityId}")
            .GetJsonAsync<FacilityModel>(cancellationToken: cancellationToken);

    public Task DeleteAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"/Facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken);
}
