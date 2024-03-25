using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config;

public class SaveConfigEntityCommand : IRequest
{
    public string? FacilityId { get; set; }
    public SaveTypeSource Source { get; set; }
    public NormalizationConfigModel NormalizationConfigModel { get; set; }
}

public class SaveConfigEntityCommandHandler : IRequestHandler<SaveConfigEntityCommand>
{
    private readonly ILogger<SaveConfigEntityCommandHandler> _logger;
    private readonly IConfigRepository _configRepo;
    private readonly ITenantApiService _tenantApiService;

    public SaveConfigEntityCommandHandler(ILogger<SaveConfigEntityCommandHandler> logger, IConfigRepository configRepo, ITenantApiService tenantApiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
        _tenantApiService = tenantApiService;
    }

    public async Task Handle(SaveConfigEntityCommand request, CancellationToken cancellationToken)
    {
        if(request.Source == SaveTypeSource.Update && string.IsNullOrWhiteSpace(request.FacilityId))
        {
            var message = "FacilityId property is null in request. Unable to proceed with update.";
            throw new ConfigOperationNullException(message);
        }

        if(request.NormalizationConfigModel == null 
            || string.IsNullOrWhiteSpace(request.NormalizationConfigModel.FacilityId) 
            || request.NormalizationConfigModel.OperationSequence == null
            || request.NormalizationConfigModel.OperationSequence.Count == 0)
        {
            var message = "Configuration provided is not valid.";
            throw new ConfigOperationNullException(message);
        }

        bool tenantExists;
        try
        {
            tenantExists = await _tenantApiService.CheckFacilityExists(request.NormalizationConfigModel.FacilityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking if facility ({request.FacilityId}) exists in Tenant Service.");
            throw;
        }

        if (!tenantExists)
        {
            throw new TenantNotFoundException($"{request.NormalizationConfigModel.FacilityId} not found in Tenant Service.");
        }

        var existingEntity = await _configRepo.GetAsync(request.Source == SaveTypeSource.Create ? request.NormalizationConfigModel.FacilityId : request.FacilityId);

        if(request.Source == SaveTypeSource.Create)
        {
            if(existingEntity != null)
            {
                throw new EntityAlreadyExistsException();
            }

            var utcDate = DateTime.UtcNow;
            var entity = new NormalizationConfigEntity
            {
                FacilityId = request.NormalizationConfigModel.FacilityId,
                OperationSequence = request.NormalizationConfigModel.OperationSequence,
                CreatedDate = utcDate,
                ModifiedDate = utcDate,
            };

            await _configRepo.AddAsync(entity);
        }
        else
        {
            if (existingEntity == null)
            {
                throw new NoEntityFoundException();
            }

            existingEntity.OperationSequence = request.NormalizationConfigModel.OperationSequence;
            existingEntity.ModifiedDate = DateTime.UtcNow;

            await _configRepo.UpdateAsync(existingEntity, cancellationToken);
        }
    }
}
