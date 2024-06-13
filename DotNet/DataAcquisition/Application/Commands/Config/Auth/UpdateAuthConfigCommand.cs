using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;

public class UpdateAuthConfigCommand : IRequest<AuthenticationConfiguration>
{
    public string FacilityId { get; set; }
    public AuthenticationConfiguration Configuration { get; set; }
    public QueryConfigurationTypePathParameter? QueryConfigurationTypePathParameter { get; set; }
}

public class UpdateAuthConfigCommandHandler : IRequestHandler<UpdateAuthConfigCommand, AuthenticationConfiguration>
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

    public async Task<AuthenticationConfiguration> Handle(UpdateAuthConfigCommand request, CancellationToken cancellationToken = default)
    {
        if (request.QueryConfigurationTypePathParameter == null)
        {
            throw new BadRequestException("QueryConfigTypeParam is null.");
        }

        if (request.FacilityId == null)
        {
            throw new BadRequestException("FacilityId is null.");
        }

        if (await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.FacilityId }, cancellationToken) == false)
        {
            throw new NotFoundException($"Facility {request.FacilityId} not found.");
        }

        AuthenticationConfiguration created;
        if (request.QueryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
        {
            created = await _fhirQueryConfigurationRepository.UpdateAuthenticationConfiguration(request.FacilityId, request.Configuration, cancellationToken);
        }
        else
        {
            created = await _fhirQueryListConfigurationRepository.UpdateAuthenticationConfiguration(request.FacilityId, request.Configuration, cancellationToken);
        }

        return created;
    }
}
