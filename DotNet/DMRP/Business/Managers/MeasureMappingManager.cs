using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.DMRP.Business.Managers
{
    public interface IMeasureMappingManager
    {
        Task<MeasureMapping> CreateAsync(MeasureMapping newMeasureMapping, CancellationToken cancellationToken = default);
        Task UpdateAsync(string id, MeasureMapping measureMapping, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task DeleteAllAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class DuplicateMeasureMappingException : ApplicationException
    {
        public DuplicateMeasureMappingException(Exception innerException)
            : base("A measure mapping with this measure and DQM already exists.", innerException)
        {
        }
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
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new DuplicateMeasureMappingException(ex);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException("Measure mapping failed to create. " + ex.Message, ex);
            }

            return newMeasureMapping;
        }

        private static bool IsUniqueIndexViolation(DbUpdateException exception) =>
            exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 }
            || exception.InnerException is SqlException { Number: 2601 or 2627 };

        public async Task UpdateAsync(string id, MeasureMapping measureMapping, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Measure Mapping");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Measure mapping with Id: {Id} not found", id.SanitizeForLog());
                throw new ApplicationException($"Measure mapping with Id: {id} not found");
            }

            existing.Measure = measureMapping.Measure;
            existing.DQM = measureMapping.DQM;
            existing.Frequency = measureMapping.Frequency;

            try
            {
                _repository.Update(existing);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new DuplicateMeasureMappingException(ex);
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
                _logger.LogError("Measure mapping with Id: {Id} not found", id.SanitizeForLog());
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

        public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete All Measure Mappings");

            try
            {
                var measureMappings = await _repository.GetAllAsync(cancellationToken);
                foreach (var measureMapping in measureMappings)
                {
                    _repository.Remove(measureMapping);
                }
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
                throw new ApplicationException("Failed to delete all measure mappings. " + ex.Message);
            }
        }
    }
}
