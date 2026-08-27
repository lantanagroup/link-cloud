using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

internal sealed class CensusConfigurationGateway : ICensusConfigurationGateway
{
    private const string ServiceName = "Census";

    private readonly ICensusServiceClient _censusClient;

    public CensusConfigurationGateway(ICensusServiceClient censusClient)
    {
        _censusClient = censusClient;
    }

    public async Task<string?> GetAcquisitionFrequencyAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _censusClient.GetCensusConfigAsync(facilityId, cancellationToken);
        var config = LinkResponseHandler.Optional(response, ServiceName, nameof(GetAcquisitionFrequencyAsync));

        return config?.ScheduledTrigger;
    }
}
