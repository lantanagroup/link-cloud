using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository.Mappings;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
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

            await ValidateAsync(newFacilityReportingPlan, cancellationToken);

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

            await ValidateAsync(facilityReportingPlan, cancellationToken);

            existing.FacilityId = facilityReportingPlan.FacilityId;
            existing.MeasureMappingId = facilityReportingPlan.MeasureMappingId;
            existing.Measure = facilityReportingPlan.Measure;
            existing.Component = facilityReportingPlan.Component;
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
        /// Rejects a reporting plan that cannot be stored. Does not check for a duplicate period:
        /// that is left to the unique index, and reported by <see cref="TranslateSaveFailureAsync"/>
        /// when the save fails.
        /// </summary>
        private async Task ValidateAsync(FacilityReportingPlan plan, CancellationToken cancellationToken)
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

            if (!ReportingComponents.IsKnown(plan.Component))
            {
                throw new ReportingPlanValidationException(
                    $"Component must be one of: {string.Join(", ", ReportingComponents.All)}.");
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

            // The mapping is optional, but naming one that does not exist is still a mistake.
            // When it is supplied and the caller left the measure blank, the mapping is where the
            // measure name comes from - which is what keeps callers that only know about mappings
            // working unchanged.
            if (!string.IsNullOrWhiteSpace(plan.MeasureMappingId))
            {
                var mapping = await _measureMappingRepository.FirstOrDefaultAsync(
                    m => m.Id == plan.MeasureMappingId, cancellationToken);

                if (mapping is null)
                {
                    throw new ReportingPlanValidationException($"Measure mapping with Id: {plan.MeasureMappingId} not found.");
                }

                if (string.IsNullOrWhiteSpace(plan.Measure))
                {
                    plan.Measure = mapping.Measure;
                }
                else if (!string.Equals(plan.Measure, mapping.Measure, StringComparison.OrdinalIgnoreCase))
                {
                    // The measure is the fact DMRP reported; the mapping is an admin's later decision
                    // about how to evaluate it. Letting the two contradict each other would store a row
                    // claiming one measure while scheduling another's dQM, and would put the same
                    // mapping and period in the table twice under two different measure names -- the
                    // unique key is on the measure, so it would not stop it.
                    throw new ReportingPlanValidationException(
                        $"Measure '{plan.Measure}' does not match measure mapping {plan.MeasureMappingId}, which is for '{mapping.Measure}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(plan.Measure))
            {
                throw new ReportingPlanValidationException(
                    "Measure is required when no measure mapping is supplied to take it from.");
            }

            if (plan.Measure.Length > FacilityReportingPlanConfigMap.MeasureMaxLength)
            {
                throw new ReportingPlanValidationException(
                    $"Measure must be {FacilityReportingPlanConfigMap.MeasureMaxLength} characters or fewer.");
            }

            var facilityExists = await _facilityExistence.ExistsAsync(plan.FacilityId, cancellationToken);

            if (!facilityExists)
            {
                throw new ReportingPlanValidationException($"Facility with Id: {plan.FacilityId} not found.");
            }
        }

        /// <summary>
        /// Matches the unique index: an enrollment is the same one when the facility, component,
        /// measure and period are the same. Keyed on the measure rather than the mapping, because
        /// the mapping is optional and mapping one afterwards must not change what counts as a
        /// duplicate.
        /// </summary>
        private Task<bool> IsDuplicateAsync(FacilityReportingPlan plan, string? currentId, CancellationToken cancellationToken) =>
            _repository.AnyAsync(p => p.FacilityId == plan.FacilityId
                && p.Component == plan.Component
                && p.Measure == plan.Measure
                && p.ReportingMonth == plan.ReportingMonth
                && p.ReportingYear == plan.ReportingYear
                && (currentId == null || p.Id != currentId), cancellationToken);

        private async Task<Exception?> TranslateSaveFailureAsync(FacilityReportingPlan plan, string? currentId,
            DbUpdateException ex, CancellationToken cancellationToken)
        {
            // The exception says so outright on SQL Server, which saves asking the database a second
            // time. Providers that do not report it recognisably fall back to the query.
            if (IsUniquePeriodViolation(ex) || await IsDuplicateAsync(plan, currentId, cancellationToken))
            {
                _logger.LogWarning(ex, "Reporting plan for facility {FacilityId} lost a race for the unique period index",
                    plan.FacilityId.SanitizeForLog());

                return new DuplicateReportingPlanException(plan.FacilityId, plan.Component, plan.Measure,
                    plan.ReportingMonth, plan.ReportingYear);
            }

            return null;
        }

        // SQL Server: 2627 = unique constraint, 2601 = unique index. EF wraps the provider exception,
        // sometimes several levels deep, so walk the chain.
        private static bool IsUniquePeriodViolation(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException { Number: 2601 or 2627 })
                {
                    return true;
                }

                if (current.Message.Contains(FacilityReportingPlanConfigMap.UniquePeriodIndexName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
