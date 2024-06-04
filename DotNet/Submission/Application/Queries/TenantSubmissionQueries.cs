using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;
using LantanaGroup.Link.Submission.Application.Repositories;
using LantanaGroup.Link.Submission.Domain.Entities;

namespace LantanaGroup.Link.Submission.Application.Queries
{
    public class TenantSubmissionQueries : ITenantSubmissionQueries
    {
        private readonly TenantSubmissionRepository _repository;
        private readonly ILogger<TenantSubmissionQueries> _logger;
        public TenantSubmissionQueries(TenantSubmissionRepository repository, ILogger<TenantSubmissionQueries> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        private static TenantSubmissionConfigEntity ModelToEntity(TenantSubmissionConfig model)
        {
            return new TenantSubmissionConfigEntity()
            {
                Id = new TenantSubmissionConfigEntityId(new Guid(model.Id)),
                FacilityId = model.FacilityId,
                CreateDate = model.CreateDate,
                ModifyDate = model.ModifyDate,
                ReportType = model.ReportType,
                Methods = model.Methods.Select(m => new Method()
                {
                    CreateDate = m.CreateDate ?? DateTime.UtcNow,
                    ModifyDate = m.ModifyDate
                    //TODO: Auth and Protocol handling
                }).ToList()
            };
        }

        private static TenantSubmissionConfig EntityToModel(TenantSubmissionConfigEntity entity)
        {
            return new TenantSubmissionConfig()
            {
                Id = entity.Id.ToString(),
                FacilityId = entity.FacilityId,
                CreateDate = entity.CreateDate,
                ModifyDate = entity.ModifyDate,
                ReportType = entity.ReportType,
                Methods = entity.Methods.Select(m => new Method()
                {
                    CreateDate = m.CreateDate,
                    ModifyDate = m.ModifyDate
                    //TODO: Auth and Protocol handling
                }).ToList()
            };
        }

        public async Task<TenantSubmissionConfig> FindTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await _repository.FindAsync(facilityId, cancellationToken);

                if (existing == null)
                {
                    return null;
                }

                return EntityToModel(existing);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(FindTenantSubmissionConfig)} exception: {ex.Message}");
            }

            return null;
        }

        public async Task<TenantSubmissionConfig> GetTenantSubmissionConfig(string configId,  CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await _repository.GetAsync(configId, cancellationToken);

                if (existing == null)
                {
                    return null;
                }

                return EntityToModel(existing);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(GetTenantSubmissionConfig)} exception: {ex.Message}");
                throw;
            }
        }

        public async Task<TenantSubmissionConfig> CreateTenantSubmissionConfig(TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await FindTenantSubmissionConfig(tenantSubmissionConfig.FacilityId, cancellationToken);
                if (existing != null)
                {
                    return null;
                }

                tenantSubmissionConfig.CreateDate = DateTime.UtcNow;
                var entity = ModelToEntity(tenantSubmissionConfig);
                await _repository.AddAsync(entity, cancellationToken);
                return EntityToModel(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(CreateTenantSubmissionConfig)} exception: {ex.Message}");
                throw;
            }
        }

        public async Task<TenantSubmissionConfig> UpdateTenantSubmissionConfig(TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken)
        {
            try
            {
                var existing = await _repository.GetAsync(tenantSubmissionConfig.Id, cancellationToken);

                if (existing == null)
                {
                    return null;
                }

                existing.ReportType = tenantSubmissionConfig.ReportType;
                existing.Methods = tenantSubmissionConfig.Methods;
                existing.FacilityId = tenantSubmissionConfig.FacilityId;
                existing.ModifyDate = DateTime.UtcNow;

                var updated = await _repository.UpdateAsync(existing, cancellationToken);
                return EntityToModel(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(UpdateTenantSubmissionConfig)} exception: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteTenantSubmissionConfig(string configId, CancellationToken cancellationToken)
        {
            bool returnVal = false;
            try
            {
                await _repository.DeleteAsync(configId, cancellationToken);
                returnVal = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(DeleteTenantSubmissionConfig)} exception: {ex.Message}");
                throw;
            }

            return returnVal;
        }
    }
}
