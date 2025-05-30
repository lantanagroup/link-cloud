using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Normalization.Application.Managers
{
    public class SaveConfigEntityCommand
    {
        public string? FacilityId { get; set; }
        public SaveTypeSource Source { get; set; }
        public NormalizationConfigModel NormalizationConfigModel { get; set; }
    }

    public interface INormalizationConfigManager
    {
        Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
        Task<NormalizationConfig> SingleOrDefaultAsync(Expression<Func<NormalizationConfig, bool>> predicate, CancellationToken cancellationToken = default);
        Task<NormalizationConfig> SaveConfigEntity(SaveConfigEntityCommand request, CancellationToken cancellationToken = default);
    }

    public class NormalizationConfigManager : INormalizationConfigManager
    {
        private readonly IBaseEntityRepository<NormalizationConfig> _repository;
        private readonly ITenantApiService _tenantApiService;
        private readonly ILogger<NormalizationConfigManager> _logger;
        public NormalizationConfigManager(ILogger<NormalizationConfigManager> logger, IBaseEntityRepository<NormalizationConfig> repository, ITenantApiService tenantApiService)
        {
            _repository = repository;
            _tenantApiService = tenantApiService;
            _logger = logger;
        }

        public async Task<NormalizationConfig> SingleOrDefaultAsync(Expression<Func<NormalizationConfig, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _repository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async System.Threading.Tasks.Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.SingleAsync(c => c.FacilityId == facilityId, cancellationToken);
            await _repository.DeleteAsync(entity.Id, cancellationToken);
        }

        public async Task<NormalizationConfig> SaveConfigEntity(SaveConfigEntityCommand request, CancellationToken cancellationToken = default)
        {
            if (request.Source == SaveTypeSource.Update && string.IsNullOrWhiteSpace(request.FacilityId))
            {
                var message = "FacilityId property is null in request. Unable to proceed with update.";
                throw new ConfigOperationNullException(message);
            }

            if (request.NormalizationConfigModel == null
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
                _logger.LogError(ex, "Exception in NormalizationConfigManager.SaveConfigEntity");
                throw;
            }

            if (!tenantExists)
            {
                throw new TenantNotFoundException($"{request.NormalizationConfigModel.FacilityId} not found in Tenant Service.");
            }

            var facilityId = request.Source == SaveTypeSource.Create
                ? request.NormalizationConfigModel.FacilityId
                : request.FacilityId;

            var existingEntity = await _repository.FirstOrDefaultAsync(c => c.FacilityId == facilityId, cancellationToken);

            if (request.Source == SaveTypeSource.Create)
            {
                if (existingEntity != null)
                {
                    throw new EntityAlreadyExistsException();
                }

                var utcDate = DateTime.UtcNow;
                var entity = new NormalizationConfig
                {
                    FacilityId = request.NormalizationConfigModel.FacilityId,
                    OperationSequence = request.NormalizationConfigModel.OperationSequence,
                    CreateDate = utcDate,
                    ModifyDate = utcDate,
                };

                return await _repository.AddAsync(entity, cancellationToken);
            }

            if (existingEntity == null)
            {
                throw new NoEntityFoundException();
            }

            existingEntity.OperationSequence = request.NormalizationConfigModel.OperationSequence;
            existingEntity.ModifyDate = DateTime.UtcNow;

            return await _repository.UpdateAsync(existingEntity, cancellationToken);
        }

    }
}
