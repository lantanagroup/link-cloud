using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class DeleteQueryPlanCommand : IRequest<Unit>
{
    public string FacilityId { get; set; }
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
        await _queryPlanRepository.DeleteAsync(request.FacilityId, cancellationToken);

        return new Unit();
    }
}
