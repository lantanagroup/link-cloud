using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;

public class DeleteFhirQueryConfigurationCommand : IRequest<Unit>
{
    public string FacilityId { get; set; }
}

public class DeleteFhirQueryConfigurationCommandHandler : IRequestHandler<DeleteFhirQueryConfigurationCommand, Unit>
{
    private readonly ILogger<DeleteFhirQueryConfigurationCommandHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _repository;

    public DeleteFhirQueryConfigurationCommandHandler(
        ILogger<DeleteFhirQueryConfigurationCommandHandler> logger, 
        IFhirQueryConfigurationRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Unit> Handle(DeleteFhirQueryConfigurationCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.FacilityId, cancellationToken);
        return new Unit();
    }
}