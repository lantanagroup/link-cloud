using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config;

public class DeleteConfigCommand : IRequest<Unit>
{
    public string FacilityId { get; set; }
}

public class DeleteConfigCommandHandler : IRequestHandler<DeleteConfigCommand, Unit>
{
    private readonly ILogger<DeleteConfigCommandHandler> _logger;
    private readonly DataAcqTenantConfigMongoRepo _dataAcqTenantConfigMongoRepo;

    public DeleteConfigCommandHandler(ILogger<DeleteConfigCommandHandler> logger, DataAcqTenantConfigMongoRepo dataAcqTenantConfigMongoRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcqTenantConfigMongoRepo = dataAcqTenantConfigMongoRepo ?? throw new ArgumentNullException(nameof(dataAcqTenantConfigMongoRepo));
    }

    public async Task<Unit> Handle(DeleteConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Deleting config for facility: {request.FacilityId}");
        await _dataAcqTenantConfigMongoRepo.DeleteAsync(request.FacilityId, cancellationToken);
        return new Unit();
    }
}
