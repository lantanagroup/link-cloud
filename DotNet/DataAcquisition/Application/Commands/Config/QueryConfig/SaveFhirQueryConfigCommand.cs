using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;

public class SaveFhirQueryConfigCommand : IRequest<FhirQueryConfiguration>
{
    public FhirQueryConfiguration queryConfiguration { get; set; }
}

public class SaveFhirQueryConfigCommandHandler : IRequestHandler<SaveFhirQueryConfigCommand, FhirQueryConfiguration>
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

    public async Task<FhirQueryConfiguration> Handle(SaveFhirQueryConfigCommand request, CancellationToken cancellationToken)
    {
        if (await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.queryConfiguration.FacilityId }, cancellationToken) == false)
        {
            throw new NotFoundException($"Facility {request.queryConfiguration.FacilityId} not found.");
        }

        if (request.queryConfiguration.ModifyDate == null)
        {
            request.queryConfiguration.ModifyDate = DateTime.UtcNow;
        }

        var existingConfig = await _mediator.Send(new GetFhirQueryConfigQuery
        {
            FacilityId = request.queryConfiguration.FacilityId,
        }, cancellationToken);

        FhirQueryConfiguration result;
        if (existingConfig == null) 
        {
            result = await _queryConfigurationRepository.AddAsync(request.queryConfiguration, cancellationToken);
        }
        else
        {
            result = await _queryConfigurationRepository.UpdateAsync(request.queryConfiguration, cancellationToken);
        }

        return result;
    }
}
