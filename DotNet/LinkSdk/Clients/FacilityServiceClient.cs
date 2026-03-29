using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class FacilityServiceClient : LinkApiClientBase
{
    public FacilityServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<HttpStatusCode> CreateAsync(FacilityModel request, CancellationToken cancellationToken = default)
    {
        var response = await Request("/Facility")
            .AllowAnyHttpStatus()
            .PostJsonAsync(request, cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"/Facility/{facilityId}")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"/Facility/{facilityId}")
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<(HttpStatusCode StatusCode, FacilityModel? Response)> GetDetailsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"/Facility/{facilityId}")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<FacilityModel>(response));
    }
}
