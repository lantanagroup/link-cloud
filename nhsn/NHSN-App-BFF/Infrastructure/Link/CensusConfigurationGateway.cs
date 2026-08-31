using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;

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

    public async Task SaveAcquisitionFrequencyAsync(string facilityId, string scheduledTrigger, CancellationToken cancellationToken = default)
    {
        var response = await _censusClient.GetCensusConfigAsync(facilityId, cancellationToken);
        var current = LinkResponseHandler.Optional(response, ServiceName, nameof(SaveAcquisitionFrequencyAsync));

        if (current is null)
        {
            var created = new CensusConfigApiModel { FacilityId = facilityId, ScheduledTrigger = scheduledTrigger };
            var createResponse = await _censusClient.CreateCensusConfigAsync(created, cancellationToken);
            LinkResponseHandler.Require(createResponse, ServiceName, nameof(SaveAcquisitionFrequencyAsync));
            return;
        }

        // Enabled is carried through from the fetched instance and never assigned - it is the
        // arming switch, set only by the completion fan-out.
        current.ScheduledTrigger = scheduledTrigger;
        var updateResponse = await _censusClient.UpdateCensusConfigAsync(facilityId, current, cancellationToken);
        LinkResponseHandler.Require(updateResponse, ServiceName, nameof(SaveAcquisitionFrequencyAsync));
    }
}
