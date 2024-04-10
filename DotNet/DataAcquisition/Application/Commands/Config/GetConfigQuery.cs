using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config;

public class GetConfigQuery : IRequest<TenantDataAcquisitionConfigModel>
{
    public string FacilityId { get; set; }
}

public class GetConfigQueryHandler : IRequestHandler<GetConfigQuery, TenantDataAcquisitionConfigModel>
{
    private readonly ILogger<GetConfigQueryHandler> _logger;
    private readonly DataAcqTenantConfigMongoRepo _dataAcqTenantConfigMongoRepo;

    public GetConfigQueryHandler(ILogger<GetConfigQueryHandler> logger, DataAcqTenantConfigMongoRepo dataAcqTenantConfigMongoRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcqTenantConfigMongoRepo = dataAcqTenantConfigMongoRepo ?? throw new ArgumentNullException(nameof(dataAcqTenantConfigMongoRepo));
    }

    public async Task<TenantDataAcquisitionConfigModel> Handle(GetConfigQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Getting config for facility: {request.FacilityId}");
        var config = await _dataAcqTenantConfigMongoRepo.GetConfigByFacilityId(request.FacilityId, cancellationToken);
        return config;
    }
}
