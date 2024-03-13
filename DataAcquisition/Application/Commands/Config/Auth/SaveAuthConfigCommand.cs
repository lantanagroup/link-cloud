using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;

public class SaveAuthConfigCommand : IRequest<Unit>
{
    public string FacilityId { get; set; }
    public AuthenticationConfiguration Configuration { get; set; }
    public QueryConfigurationTypePathParameter? QueryConfigurationTypePathParameter { get; set; }
}

public class UpdateAuthConfigCommandHandler : IRequestHandler<SaveAuthConfigCommand, Unit>
{
    private readonly ILogger<UpdateAuthConfigCommandHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _fhirQueryConfigurationRepository;
    private readonly IFhirQueryListConfigurationRepository _fhirQueryListConfigurationRepository;
    private readonly IMediator _mediator;

    public UpdateAuthConfigCommandHandler(
        ILogger<UpdateAuthConfigCommandHandler> logger,
        IFhirQueryConfigurationRepository fhirQueryConfigurationRepository,
        IFhirQueryListConfigurationRepository fhirQueryListConfigurationRepository,
        IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryConfigurationRepository = fhirQueryConfigurationRepository ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationRepository));
        _fhirQueryListConfigurationRepository = fhirQueryListConfigurationRepository ?? throw new ArgumentNullException(nameof(fhirQueryListConfigurationRepository));
        _mediator = mediator;
    }

    public async Task<Unit> Handle(SaveAuthConfigCommand request, CancellationToken cancellationToken = default)
    {
        if (request.QueryConfigurationTypePathParameter == null)
        {
            _logger.LogWarning("{QueryConfigTypeParam} is null.", nameof(request.QueryConfigurationTypePathParameter));
            return new Unit();
        }

        if (request.FacilityId == null)
        {
            _logger.LogWarning("{FacilityId} is null.", nameof(request.FacilityId));
            return new Unit();
        }

        if(await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.FacilityId }, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {request.FacilityId} not found.");
        }

        if (request.QueryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
        {
            if(!(await checkIfFacilityConfigExists(request.FacilityId, request.QueryConfigurationTypePathParameter.Value, cancellationToken)))
                throw new MissingTenantConfigurationException($"Facility configuration for {request.FacilityId} does not exist.");
            await _fhirQueryConfigurationRepository.SaveAuthenticationConfiguration(request.FacilityId, request.Configuration, cancellationToken);
        }
        else
        {
            if (!(await checkIfFacilityConfigExists(request.FacilityId, request.QueryConfigurationTypePathParameter.Value, cancellationToken)))
                throw new MissingTenantConfigurationException($"Facility configuration for {request.FacilityId} does not exist.");
            await _fhirQueryListConfigurationRepository.SaveAuthenticationConfiguration(request.FacilityId, request.Configuration, cancellationToken);
        }
        return new Unit();
    }

    private async Task<bool> checkIfFacilityConfigExists(string facilityId, QueryConfigurationTypePathParameter queryConfigurationTypePathParameter, CancellationToken cancellationToken = default)
    {
        if (queryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
        {
            return await _fhirQueryConfigurationRepository.GetAsync(facilityId) != null;
        }
        else
        {
            return await _fhirQueryListConfigurationRepository.GetAsync(facilityId) != null;
        }
    }
}
