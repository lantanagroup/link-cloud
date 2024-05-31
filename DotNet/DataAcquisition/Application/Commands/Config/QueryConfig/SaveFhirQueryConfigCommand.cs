using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
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
    private readonly ITenantApiService _tenantApiService;

    public SaveFhirQueryConfigCommandHandler(ILogger<SaveFhirQueryConfigCommandHandler> logger, IFhirQueryConfigurationRepository queryConfigurationRepository, IMediator mediator, ITenantApiService tenantApiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryConfigurationRepository = queryConfigurationRepository ?? throw new ArgumentNullException(nameof(queryConfigurationRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
    }

    public async Task<Unit> Handle(SaveFhirQueryConfigCommand request, CancellationToken cancellationToken)
    {
        if (await _tenantApiService.CheckFacilityExists(request.queryConfiguration.FacilityId, cancellationToken) == false)
        {
            _logger.LogWarning("Facility {facilityId} not found in Tenant service.", request.queryConfiguration.FacilityId);
            throw new MissingFacilityConfigurationException($"Facility {request.queryConfiguration.FacilityId} not found.");
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
            _logger.LogInformation("Query configuration for {facilityId} was created successfully.", request.queryConfiguration.FacilityId);
        }
        else
        {
            await _queryConfigurationRepository.UpdateAsync(request.queryConfiguration, cancellationToken);
            _logger.LogInformation("Query configuration for {facilityId} was updated successfully.", request.queryConfiguration.FacilityId);
        }        

        return new Unit();
    }
}
