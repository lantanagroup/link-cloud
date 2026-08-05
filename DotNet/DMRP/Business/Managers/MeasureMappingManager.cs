using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.DMRP.Business.Managers
{
    public interface IMeasureMappingManager
    {
        Task<MeasureMapping> CreateAsync(MeasureMapping newMeasureMapping, CancellationToken cancellationToken = default);
        Task UpdateAsync(string id, MeasureMapping measureMapping, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }

    public class MeasureMappingManager : IMeasureMappingManager
    {
        private readonly ILogger<MeasureMappingManager> _logger;
        private readonly IEntityRepository<MeasureMapping> _repository;

        public MeasureMappingManager(ILogger<MeasureMappingManager> logger, IEntityRepository<MeasureMapping> repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureMapping> CreateAsync(MeasureMapping newMeasureMapping, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Measure Mapping");

            try
            {
                await _repository.AddAsync(newMeasureMapping, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException("Measure mapping failed to create. " + ex.Message);
            }

            return newMeasureMapping;
        }

        public async Task UpdateAsync(string id, MeasureMapping measureMapping, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Measure Mapping");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Measure mapping with Id: {Id} not found", id);
                throw new ApplicationException($"Measure mapping with Id: {id} not found");
            }

            // TODO: Map `measureMapping` to `existing`

            try
            {
                _repository.Update(existing);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException($"Measure mapping {id} failed to update. " + ex.Message);
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Measure Mapping");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Measure mapping with Id: {Id} not found", id);
                throw new ApplicationException($"Measure mapping with Id: {id} not found");
            }

            try
            {
                _repository.Remove(existing);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException($"Measure mapping {id} failed to delete. " + ex.Message);
            }
        }
    }
}
