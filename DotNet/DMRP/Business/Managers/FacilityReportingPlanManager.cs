using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository.Mappings;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.DMRP.Business.Managers
{
    public interface IFacilityReportingPlanManager
    {
        Task<FacilityReportingPlan> CreateAsync(FacilityReportingPlan newFacilityReportingPlan, CancellationToken cancellationToken = default);
        Task UpdateAsync(string id, FacilityReportingPlan facilityReportingPlan, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>Removes every reporting plan. Returns the number of rows removed.</summary>
        Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Removes the reporting plans of one facility. Returns the number of rows removed.</summary>
        Task<int> DeleteForFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    }

    public class FacilityReportingPlanManager : IFacilityReportingPlanManager
    {
        internal const int MinimumReportingYear = ReportingPeriodLimits.MinimumReportingYear;
        internal const int MaximumReportingYear = ReportingPeriodLimits.MaximumReportingYear;

        private readonly ILogger<FacilityReportingPlanManager> _logger;
        private readonly IEntityRepository<FacilityReportingPlan> _repository;
        private readonly IEntityRepository<MeasureMapping> _measureMappingRepository;
        private readonly IFacilityExistence _facilityExistence;

        public FacilityReportingPlanManager(ILogger<FacilityReportingPlanManager> logger,
            IEntityRepository<FacilityReportingPlan> repository,
            IEntityRepository<MeasureMapping> measureMappingRepository,
            IFacilityExistence facilityExistence)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _measureMappingRepository = measureMappingRepository ?? throw new ArgumentNullException(nameof(measureMappingRepository));
            _facilityExistence = facilityExistence ?? throw new ArgumentNullException(nameof(facilityExistence));
        }

        public async Task<FacilityReportingPlan> CreateAsync(FacilityReportingPlan newFacilityReportingPlan, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Facility Reporting Plan");

            ArgumentNullException.ThrowIfNull(newFacilityReportingPlan);

            await ValidateAsync(newFacilityReportingPlan, null, cancellationToken);

            try
            {
                await _repository.AddAsync(newFacilityReportingPlan, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DbUpdateException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddException(ex);

                var translated = await TranslateSaveFailureAsync(newFacilityReportingPlan, null, ex, cancellationToken);
                if (translated is null)
                {
                    throw;
                }

                throw translated;
            }

            return newFacilityReportingPlan;
        }

        public async Task UpdateAsync(string id, FacilityReportingPlan facilityReportingPlan, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Reporting Plan");

            ArgumentNullException.ThrowIfNull(facilityReportingPlan);

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Facility reporting plan with Id: {Id} not found", id.SanitizeForLog());
                throw new KeyNotFoundException($"Facility reporting plan with Id: {id} not found");
            }

            await ValidateAsync(facilityReportingPlan, id, cancellationToken);

            existing.FacilityId = facilityReportingPlan.FacilityId;
            existing.MeasureMappingId = facilityReportingPlan.MeasureMappingId;
            existing.ReportingMonth = facilityReportingPlan.ReportingMonth;
            existing.ReportingYear = facilityReportingPlan.ReportingYear;
            existing.IsReporting = facilityReportingPlan.IsReporting;

            try
            {
                _repository.Update(existing);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DbUpdateException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddException(ex);

                var translated = await TranslateSaveFailureAsync(existing, id, ex, cancellationToken);
                if (translated is null)
                {
                    throw;
                }

                throw translated;
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Reporting Plan");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Facility reporting plan with Id: {Id} not found", id.SanitizeForLog());
                throw new KeyNotFoundException($"Facility reporting plan with Id: {id} not found");
            }

            _repository.Remove(existing);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete All Facility Reporting Plans");

            return _repository.ExecuteDeleteAsync(_ => true, cancellationToken);
        }

        public async Task<int> DeleteForFacilityAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Reporting Plans For Facility");

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new ReportingPlanValidationException("FacilityId is required.");
            }

            var removed = await _repository.ExecuteDeleteAsync(p => p.FacilityId == facilityId, cancellationToken);

            _logger.LogInformation("Removed {Count} reporting plan(s) for facility {FacilityId}",
                removed, facilityId.SanitizeForLog());

            return removed;
        }

        /// <summary>
        /// Rejects a reporting plan that cannot be stored. <paramref name="currentId"/> is the row
        /// being updated, which is excluded from the duplicate check so that saving a row over itself
        /// is not treated as a collision.
        /// </summary>
        private async Task ValidateAsync(FacilityReportingPlan plan, string? currentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(plan.FacilityId))
            {
                throw new ReportingPlanValidationException("FacilityId is required.");
            }

            if (plan.FacilityId.Length > FacilityReportingPlanConfigMap.FacilityIdMaxLength)
            {
                throw new ReportingPlanValidationException(
                    $"FacilityId must be {FacilityReportingPlanConfigMap.FacilityIdMaxLength} characters or fewer.");
            }

            if (string.IsNullOrWhiteSpace(plan.MeasureMappingId))
            {
                throw new ReportingPlanValidationException("MeasureMappingId is required.");
            }

            if (plan.ReportingMonth is < 1 or > 12)
            {
                throw new ReportingPlanValidationException("ReportingMonth must be between 1 and 12.");
            }

            if (plan.ReportingYear is < MinimumReportingYear or > MaximumReportingYear)
            {
                throw new ReportingPlanValidationException(
                    $"ReportingYear must be between {MinimumReportingYear} and {MaximumReportingYear}.");
            }

            var measureMappingExists = await _measureMappingRepository.AnyAsync(
                m => m.Id == plan.MeasureMappingId, cancellationToken);

            if (!measureMappingExists)
            {
                throw new ReportingPlanValidationException($"Measure mapping with Id: {plan.MeasureMappingId} not found.");
            }

            var facilityExists = await _facilityExistence.ExistsAsync(plan.FacilityId, cancellationToken);

            if (!facilityExists)
            {
                throw new ReportingPlanValidationException($"Facility with Id: {plan.FacilityId} not found.");
            }

            if (await IsDuplicateAsync(plan, currentId, cancellationToken))
            {
                throw new DuplicateReportingPlanException(plan.FacilityId, plan.MeasureMappingId,
                    plan.ReportingMonth, plan.ReportingYear);
            }
        }

        private Task<bool> IsDuplicateAsync(FacilityReportingPlan plan, string? currentId, CancellationToken cancellationToken) =>
            _repository.AnyAsync(p => p.FacilityId == plan.FacilityId
                && p.MeasureMappingId == plan.MeasureMappingId
                && p.ReportingMonth == plan.ReportingMonth
                && p.ReportingYear == plan.ReportingYear
                && (currentId == null || p.Id != currentId), cancellationToken);

        private async Task<Exception?> TranslateSaveFailureAsync(FacilityReportingPlan plan, string? currentId,
            DbUpdateException ex, CancellationToken cancellationToken)
        {
            if (await IsDuplicateAsync(plan, currentId, cancellationToken))
            {
                _logger.LogWarning(ex, "Reporting plan for facility {FacilityId} lost a race for the unique period index",
                    plan.FacilityId.SanitizeForLog());

                return new DuplicateReportingPlanException(plan.FacilityId, plan.MeasureMappingId,
                    plan.ReportingMonth, plan.ReportingYear);
            }

            return null;
        }
    }
}
