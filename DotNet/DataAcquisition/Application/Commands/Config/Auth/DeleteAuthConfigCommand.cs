using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;

public class DeleteAuthConfigCommand : IRequest<Unit>
{
    public QueryConfigurationTypePathParameter QueryConfigurationTypePathParameter { get; set; }
    public string FacilityId { get; set; }
}

public class DeleteAuthConfigCommandHandler : IRequestHandler<DeleteAuthConfigCommand, Unit>
{
    private readonly ILogger<UpdateAuthConfigCommandHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _fhirQueryConfigurationRepository;
    private readonly IFhirQueryListConfigurationRepository _fhirQueryListConfigurationRepository;

    public DeleteAuthConfigCommandHandler(
        ILogger<UpdateAuthConfigCommandHandler> logger, 
        IFhirQueryConfigurationRepository fhirQueryConfigurationRepository, 
        IFhirQueryListConfigurationRepository fhirQueryListConfigurationRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryConfigurationRepository = fhirQueryConfigurationRepository ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationRepository));
        _fhirQueryListConfigurationRepository = fhirQueryListConfigurationRepository ?? throw new ArgumentNullException(nameof(fhirQueryListConfigurationRepository));
    }

    public async Task<Unit> Handle(DeleteAuthConfigCommand request, CancellationToken cancellationToken)
    {
        if (request.QueryConfigurationTypePathParameter == null)
        {
            _logger.LogWarning($"{nameof(request.QueryConfigurationTypePathParameter)} is null.");
            return new Unit();
        }

        if (request.FacilityId == null)
        {
            _logger.LogWarning($"{nameof(request.FacilityId)} is null.");
            return new Unit();
        }

        if (request.QueryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
        {
            await _fhirQueryConfigurationRepository.DeleteAuthenticationConfiguration(request.FacilityId, cancellationToken);
        }
        else
        {
            await _fhirQueryListConfigurationRepository.DeleteAuthenticationConfiguration(request.FacilityId, cancellationToken);
        }
        return new Unit();
    }
}