using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;

public class SaveFhirQueryConfigCommand : IRequest<Unit>
{
    public FhirQueryConfiguration queryConfiguration { get; set; }
}

public class SaveFhirQueryConfigCommandHandler : IRequestHandler<SaveFhirQueryConfigCommand, Unit>
{
    private readonly ILogger<SaveFhirQueryConfigCommandHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _queryConfigurationRepository;
    private readonly IMediator _mediator;

    public SaveFhirQueryConfigCommandHandler(ILogger<SaveFhirQueryConfigCommandHandler> logger, IFhirQueryConfigurationRepository queryConfigurationRepository, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryConfigurationRepository = queryConfigurationRepository ?? throw new ArgumentNullException(nameof(queryConfigurationRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<Unit> Handle(SaveFhirQueryConfigCommand request, CancellationToken cancellationToken)
    {
        if (await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.queryConfiguration.FacilityId }, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {request.queryConfiguration.FacilityId} not found.");
        }

        if (request.queryConfiguration.ModifyDate == null)
        {
            request.queryConfiguration.ModifyDate = DateTime.UtcNow;
        }

        var existingConfig = await _mediator.Send(new GetFhirQueryConfigQuery
        {
            FacilityId = request.queryConfiguration.FacilityId,
        });

        if(existingConfig == null) 
        {
            await _queryConfigurationRepository.AddAsync(request.queryConfiguration, cancellationToken);
        }
        else
        {
            await _queryConfigurationRepository.UpdateAsync(request.queryConfiguration, cancellationToken);
        }

        return new Unit();
    }
}
