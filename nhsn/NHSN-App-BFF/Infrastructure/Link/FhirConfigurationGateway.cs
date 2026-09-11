using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

internal sealed class FhirConfigurationGateway : IFhirConfigurationGateway
{
    private const string ServiceName = "DataAcquisition";

    private readonly IDataAcquisitionServiceClient _dataAcquisitionClient;

    public FhirConfigurationGateway(IDataAcquisitionServiceClient dataAcquisitionClient)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
    }

    public async Task<FhirSection?> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.GetFhirQueryConfigurationAsync(facilityId, cancellationToken);

        // Untyped response: the body arrives as a string and is deserialized here rather than by
        // the SDK. See LinkResponseHandler.OptionalFromRawBody.
        var config = LinkResponseHandler.OptionalFromRawBody<DataAcqFhirConfiguration>(response, ServiceName, nameof(GetAsync));

        return config is null ? null : FhirConfigurationMapper.ToDomain(config);
    }

    public async Task SaveAsync(FhirConfigurationSave request, CancellationToken cancellationToken = default)
    {
        var existingResponse = await _dataAcquisitionClient.GetFhirQueryConfigurationAsync(request.FacilityId, cancellationToken);
        var existing = LinkResponseHandler.OptionalFromRawBody<DataAcqFhirConfiguration>(existingResponse, ServiceName, nameof(SaveAsync));

        if (existing?.Id is null)
        {
            var createResponse = await _dataAcquisitionClient.CreateFhirQueryConfigurationAsync(new CreateFhirQueryConfigurationRequestApiModel
            {
                FacilityId = request.FacilityId,
                FhirServerBaseUrl = request.FhirServerBaseUrl,
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                MaxRetries = request.MaxRetries,
                MinAcquisitionPullTime = request.MinAcquisitionPullTime,
                MaxAcquisitionPullTime = request.MaxAcquisitionPullTime,
                TimeZone = request.TimeZone
            }, cancellationToken);

            LinkResponseHandler.EnsureSuccess(createResponse, ServiceName, nameof(SaveAsync));
            return;
        }

        var updateResponse = await _dataAcquisitionClient.UpdateFhirQueryConfigurationAsync(new UpdateFhirQueryConfigurationPayload
        {
            Id = existing.Id,
            FacilityId = request.FacilityId,
            FhirServerBaseUrl = request.FhirServerBaseUrl,
            MaxConcurrentRequests = request.MaxConcurrentRequests,
            MaxRetries = request.MaxRetries,
            MinAcquisitionPullTime = request.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = request.MaxAcquisitionPullTime,
            TimeZone = request.TimeZone
        }, cancellationToken);

        // DataAcquisition answers 304 when the PUT payload matches what's already stored — not a
        // failure, so it's excluded from the success check rather than treated as an error.
        if (updateResponse.StatusCode != StatusCodes.Status304NotModified)
        {
            LinkResponseHandler.EnsureSuccess(updateResponse, ServiceName, nameof(SaveAsync));
        }
    }

    public async Task<bool> TestConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.ValidateConnectionAsync(fhirServerBaseUrl, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
