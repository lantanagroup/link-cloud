using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;

public class GetAuthConfigQuery : IRequest<AuthenticationConfiguration?>
{
    public QueryConfigurationTypePathParameter? QueryConfigurationTypePathParameter { get; set; }
    public string FacilityId { get; set; }
}

public class GetAuthConfigQueryHandler : IRequestHandler<GetAuthConfigQuery, AuthenticationConfiguration?>
{
    private readonly ILogger<GetAuthConfigQueryHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _fhirQueryConfigurationRepository;
    private readonly IFhirQueryListConfigurationRepository _fhirQueryListConfigurationRepository;

    public GetAuthConfigQueryHandler(
        ILogger<GetAuthConfigQueryHandler> logger, 
        IFhirQueryConfigurationRepository fhirQueryConfigurationRepository, 
        IFhirQueryListConfigurationRepository fhirQueryListConfigurationRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryConfigurationRepository = fhirQueryConfigurationRepository ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationRepository));
        _fhirQueryListConfigurationRepository = fhirQueryListConfigurationRepository;
    }

    public async Task<AuthenticationConfiguration?> Handle(GetAuthConfigQuery request, CancellationToken cancellationToken)
    {
        if(request.QueryConfigurationTypePathParameter == null)
        {
            throw new BadRequestException($"request.QueryConfigurationTypePathParameter is null.");
        }

        if (request.FacilityId == null)
        {
            throw new BadRequestException($"request.FacilityId is null.");
        }

        if (request.QueryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
        {
            return await _fhirQueryConfigurationRepository.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
        }
        else
        {
            return await _fhirQueryListConfigurationRepository.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
        }
    }
}