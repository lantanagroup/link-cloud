using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models.Exceptions;
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
        private readonly IEntityRepository<FacilityReportingPlan> _reportingPlanRepository;

        public MeasureMappingManager(ILogger<MeasureMappingManager> logger,
            IEntityRepository<MeasureMapping> repository,
            IEntityRepository<FacilityReportingPlan> reportingPlanRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _reportingPlanRepository = reportingPlanRepository ?? throw new ArgumentNullException(nameof(reportingPlanRepository));
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

        // Backstop for the window between the pre-check below and the delete. Which error the database
        // raises depends on EF's change tracker, not just the schema: with the dependent untracked the
        // DELETE reaches the database and the foreign key fires (SQL Server 547, SQLite 787), but with
        // it tracked EF first tries to sever the relationship by nulling MeasureMappingId, which the
        // NOT NULL column rejects instead (SQL Server 515, SQLite 1299). Both mean the same thing
        // here. EF wraps the provider exception, at times several levels deep, so walk the chain.
        private static bool IsStillReferenced(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException { Number: 547 or 515 }
                    || current is SqliteException { SqliteExtendedErrorCode: 787 or 1299 })
                {
                    return true;
                }
            }

            return false;
        }

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

            // Asked before the delete rather than inferred from the failure afterwards: the database
            // error is not stable enough to classify on (see IsStillReferenced), and refusing up front
            // gives the caller a message that names the reason.
            if (await _reportingPlanRepository.AnyAsync(p => p.MeasureMappingId == id, cancellationToken))
            {
                _logger.LogInformation(
                    "Measure mapping {Id} is referenced by facility reporting plans and was not deleted",
                    id.SanitizeForLog());

                throw new MeasureMappingInUseException(id);
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
            catch (Exception ex) when (IsStillReferenced(ex))
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new MeasureMappingInUseException(id, ex);
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

            // Scoped to plans that actually reference a mapping, the same question the single-mapping
            // delete above asks. A plan whose measure Link has no mapping for holds no foreign key, so
            // it cannot be what stops a delete - and refusing on its account would leave a database of
            // nothing but unmapped enrollments unable to clear its mappings at all.
            if (await _reportingPlanRepository.AnyAsync(p => p.MeasureMappingId != null, cancellationToken))
            {
                _logger.LogInformation(
                    "Measure mappings are referenced by facility reporting plans and none were deleted");

                throw new MeasureMappingInUseException();
            }

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
            catch (Exception ex) when (IsStillReferenced(ex))
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new MeasureMappingInUseException(ex);
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
