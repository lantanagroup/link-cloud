using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

internal sealed class QueryDispatchGateway : IQueryDispatchGateway
{
    private const string ServiceName = "QueryDispatch";

    // The only event type Query Dispatch defines today (QueryDispatchConstants.EventType in the
    // QueryDispatch service, not reachable from here since it isn't part of LinkSdk or Shared).
    private const string DischargeEvent = "Discharge";

    private readonly IQueryDispatchServiceClient _queryDispatchClient;

    public QueryDispatchGateway(IQueryDispatchServiceClient queryDispatchClient)
    {
        _queryDispatchClient = queryDispatchClient;
    }

    public async Task<string?> GetLagDurationAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _queryDispatchClient.GetConfigurationAsync(facilityId, cancellationToken);
        var config = LinkResponseHandler.Optional(response, ServiceName, nameof(GetLagDurationAsync));

        return config?.DispatchSchedules.FirstOrDefault(x => x.Event == DischargeEvent)?.Duration;
    }

    public async Task SetLagDurationAsync(string facilityId, string lagDuration, CancellationToken cancellationToken = default)
    {
        var response = await _queryDispatchClient.UpsertQueryDispatchConfigurationAsync(facilityId, new QueryDispatchConfigurationApiModel
        {
            FacilityId = facilityId,
            DispatchSchedules = [new DispatchScheduleApiModel { Event = DischargeEvent, Duration = lagDuration }]
        }, cancellationToken);

        LinkResponseHandler.EnsureSuccess(response, ServiceName, nameof(SetLagDurationAsync));
    }
}
