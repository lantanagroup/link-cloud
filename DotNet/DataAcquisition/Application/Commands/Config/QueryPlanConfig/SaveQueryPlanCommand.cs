using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using MediatR;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class SaveQueryPlanCommand : IRequest<QueryPlan?>
{
    public string? FacilityId { get; set; }
    public QueryPlan? QueryPlan { get; set; }
}

public class SaveQueryPlanCommandHandler : IRequestHandler<SaveQueryPlanCommand, QueryPlan?>
{
    private readonly ILogger<SaveQueryPlanCommandHandler> _logger;
    private readonly IQueryPlanRepository _repository;
    private readonly IMediator _mediator;
    private readonly CompareLogic _compareLogic;

    public SaveQueryPlanCommandHandler(ILogger<SaveQueryPlanCommandHandler> logger, IQueryPlanRepository repository, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _compareLogic = new CompareLogic();
        _compareLogic.Config.MaxDifferences = 25;
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


    public async Task<QueryPlan?> Handle(SaveQueryPlanCommand request, CancellationToken cancellationToken)
    {
        if (await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.FacilityId }, cancellationToken) == false)
        {
            throw new MissingFacilityConfigurationException($"Facility {request.FacilityId} not found.");
        }

        if (string.IsNullOrEmpty(request.FacilityId))
        {
            throw new BadRequestException($"FacilityId is null or empty");
        }

        if (request.QueryPlan == null)
        {
            throw new BadRequestException($"QueryPlan is null or empty");
        }

        var plan = await _repository.GetAsync(request.FacilityId, cancellationToken);

        var queryPlan = request.QueryPlan;
        if (queryPlan != null)
        {
            if (plan == null)
            {
                plan = await _repository.AddAsync(queryPlan, cancellationToken);
            }
            else
            {
                plan = await _repository.UpdateAsync(queryPlan, cancellationToken);
            }
        }

        return plan;
    }
}
