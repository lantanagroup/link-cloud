using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;

public class GetFhirListConfigQuery : IRequest<FhirListConfiguration>
{
    public string FacilityId { get; set; }
}

public class GetFhirListConfigQueryHandler : IRequestHandler<GetFhirListConfigQuery, FhirListConfiguration>
{
    private readonly ILogger<GetFhirListConfigQueryHandler> _logger;
    private readonly IFhirQueryListConfigurationRepository _repository;

    public GetFhirListConfigQueryHandler(IFhirQueryListConfigurationRepository repository, ILogger<GetFhirListConfigQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FhirListConfiguration> Handle(GetFhirListConfigQuery request, CancellationToken cancellationToken)
    {
        if(request.FacilityId == null)
        {
            throw new ArgumentNullException(nameof(request.FacilityId));
        }

        var config = await _repository.GetByFacilityIdAsync(request.FacilityId);

        if (config == null)
        {
            throw new MissingTenantConfigurationException("No facility configuration found.");
        }

        return config;
    }
}
