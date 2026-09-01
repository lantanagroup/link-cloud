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
    private readonly IDataAcquisitionRawClient _rawClient;

    public FhirConfigurationGateway(IDataAcquisitionServiceClient dataAcquisitionClient, IDataAcquisitionRawClient rawClient)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
        _rawClient = rawClient;
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

        // Routed through the raw client rather than IDataAcquisitionServiceClient directly so the clean-replace payload shape stays owned by IDataAcquisitionRawClient
        await _rawClient.UpdateFhirQueryConfigurationAsync(new UpdateFhirQueryConfigurationPayload
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
    }
}
