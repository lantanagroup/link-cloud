using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config;

public class SaveConfigCommand : IRequest<Unit>
{
    public TenantDataAcquisitionConfigModel TenantDataAcquisitionConfigModel { get; set; }
}

public class SaveConfigCommandHandler : IRequestHandler<SaveConfigCommand, Unit>
{
    private readonly ILogger<SaveConfigCommandHandler> _logger;
    private readonly DataAcqTenantConfigMongoRepo _dataAcqTenantConfigMongoRepo;

    public SaveConfigCommandHandler(ILogger<SaveConfigCommandHandler> logger, DataAcqTenantConfigMongoRepo dataAcqTenantConfigMongoRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcqTenantConfigMongoRepo = dataAcqTenantConfigMongoRepo ?? throw new ArgumentNullException(nameof(dataAcqTenantConfigMongoRepo));
    }

    public async Task<Unit> Handle(SaveConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"upserting record for facility {request.TenantDataAcquisitionConfigModel.TenantId}");
        bool facilityExists = false;
        foreach (var facility in request.TenantDataAcquisitionConfigModel.Facilities)
        {
            var config = await _dataAcqTenantConfigMongoRepo.GetConfigByFacilityId(facility.FacilityId, cancellationToken);

            facilityExists = config != null;
            if (facilityExists)
            {
                request.TenantDataAcquisitionConfigModel.Id = config.Id;
                break;
            }
        }

        if (facilityExists)
        {
            await _dataAcqTenantConfigMongoRepo.UpdateAsync(request.TenantDataAcquisitionConfigModel, cancellationToken);
        }
        else
        {
            await _dataAcqTenantConfigMongoRepo.AddAsync(request.TenantDataAcquisitionConfigModel, cancellationToken);
        }

        return new Unit();
    }
}
