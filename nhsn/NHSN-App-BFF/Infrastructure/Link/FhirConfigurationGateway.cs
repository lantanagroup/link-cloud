using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.Clients;

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
}
