using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.Entities;
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

    public SaveConfigEntityCommandHandler(ILogger<SaveConfigEntityCommandHandler> logger, IConfigRepository configRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
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
