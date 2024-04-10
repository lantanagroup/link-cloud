using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using MediatR;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class DeleteQueryPlanCommand : IRequest<Unit>
{
    public string FacilityId { get; set; }
    public QueryPlanType QueryPlanType { get; set; }
}

public class DeleteQueryPlanCommandHandler : IRequestHandler<DeleteQueryPlanCommand, Unit>
{
    private readonly ILogger<DeleteQueryPlanCommandHandler> _logger;
    private readonly IQueryPlanRepository _queryPlanRepository;
    private readonly IMediator _mediator;

    public DeleteQueryPlanCommandHandler(ILogger<DeleteQueryPlanCommandHandler> logger, IQueryPlanRepository queryPlanRepository, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryPlanRepository = queryPlanRepository ?? throw new ArgumentNullException(nameof(queryPlanRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    private async Task SendAudit(string message, string correlationId, string facilityId, AuditEventType type, List<PropertyChangeModel> changes)
    {
        await _mediator.Send(new TriggerAuditEventCommand
        {
            AuditableEvent = new AuditEventMessage
            {
                FacilityId = facilityId,
                CorrelationId = "",
                Action = type,
                EventDate = DateTime.UtcNow,
                ServiceName = DataAcquisitionConstants.ServiceName,
                PropertyChanges = changes != null ? changes : new List<PropertyChangeModel>(),
                Resource = "DataAcquisition",
                User = "",
                UserId = "",
                Notes = $"{message}"
                
            }
        });
    }


    public async Task<Unit> Handle(DeleteQueryPlanCommand request, CancellationToken cancellationToken)
    {
        switch (request.QueryPlanType)
        {
            case QueryPlanType.QueryPlans:
                await _queryPlanRepository.DeleteAsync(request.FacilityId, cancellationToken);
                break;
            case QueryPlanType.InitialQueries:
                await _queryPlanRepository.DeleteInitialQueriesForFacility(request.FacilityId, cancellationToken);
                break;
            case QueryPlanType.SupplementalQueries:
                await _queryPlanRepository.DeleteSupplementalQueriesForFacility(request.FacilityId, cancellationToken);
                break;
        }
        await SendAudit($"Delete query plan configuration for '{request.FacilityId}'", null, request.FacilityId, AuditEventType.Delete, null);
        return new Unit();
    }
}
