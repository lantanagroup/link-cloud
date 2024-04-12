using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;

public class GetFhirQueryConfigQuery : IRequest<FhirQueryConfiguration>
{
    public string FacilityId { get; set; }
}

public class GetFhirQueryConfigQueryHandler : IRequestHandler<GetFhirQueryConfigQuery, FhirQueryConfiguration>
{
    private readonly ILogger<GetFhirQueryConfigQueryHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _repository;

    public GetFhirQueryConfigQueryHandler(ILogger<GetFhirQueryConfigQueryHandler> logger, IFhirQueryConfigurationRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<FhirQueryConfiguration> Handle(GetFhirQueryConfigQuery request, CancellationToken cancellationToken)
    {
        var result = await _repository.GetAsync(request.FacilityId, cancellationToken);
        return result;
    }
}
