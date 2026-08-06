using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository.Mappings;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
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
        /// <summary>
        /// Bounds the reporting year to something a reporting period could plausibly fall in, which
        /// catches transposed or defaulted values without hard-coding a single valid year.
        /// </summary>
        internal const int MinimumReportingYear = 2000;
        internal const int MaximumReportingYear = 2100;

        private readonly ILogger<FacilityReportingPlanManager> _logger;
        private readonly IEntityRepository<FacilityReportingPlan> _repository;
        private readonly IEntityRepository<MeasureMapping> _measureMappingRepository;
        private readonly ITenantApiService _tenantApiService;

        public FacilityReportingPlanManager(ILogger<FacilityReportingPlanManager> logger,
            IEntityRepository<FacilityReportingPlan> repository,
            IEntityRepository<MeasureMapping> measureMappingRepository,
            ITenantApiService tenantApiService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _measureMappingRepository = measureMappingRepository ?? throw new ArgumentNullException(nameof(measureMappingRepository));
            _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
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
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw await TranslateSaveFailureAsync(newFacilityReportingPlan, null, ex, cancellationToken);
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
                throw new DmrpNotFoundException($"Facility reporting plan with Id: {id} not found");
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
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw await TranslateSaveFailureAsync(existing, id, ex, cancellationToken);
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Reporting Plan");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Facility reporting plan with Id: {Id} not found", id.SanitizeForLog());
                throw new DmrpNotFoundException($"Facility reporting plan with Id: {id} not found");
            }

            _repository.Remove(existing);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete All Facility Reporting Plans");

            var existing = await _repository.GetAllAsync(cancellationToken);

            return await RemoveRangeAsync(existing, cancellationToken);
        }

        public async Task<int> DeleteForFacilityAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Reporting Plans For Facility");

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new ApplicationException("FacilityId is required.");
            }

            var existing = await _repository.FindAsync(p => p.FacilityId == facilityId, cancellationToken);

            _logger.LogInformation("Removing {Count} reporting plan(s) for facility {FacilityId}",
                existing.Count, facilityId.SanitizeForLog());

            return await RemoveRangeAsync(existing, cancellationToken);
        }

        private async Task<int> RemoveRangeAsync(List<FacilityReportingPlan> plans, CancellationToken cancellationToken)
        {
            if (plans.Count == 0)
            {
                return 0;
            }

            foreach (var plan in plans)
            {
                _repository.Remove(plan);
            }

            await _repository.SaveChangesAsync(cancellationToken);

            return plans.Count;
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
                throw new ApplicationException("FacilityId is required.");
            }

            if (plan.FacilityId.Length > FacilityReportingPlanConfigMap.FacilityIdMaxLength)
            {
                throw new ApplicationException(
                    $"FacilityId must be {FacilityReportingPlanConfigMap.FacilityIdMaxLength} characters or fewer.");
            }

            if (string.IsNullOrWhiteSpace(plan.MeasureMappingId))
            {
                throw new ApplicationException("MeasureMappingId is required.");
            }

            if (plan.ReportingMonth is < 1 or > 12)
            {
                throw new ApplicationException("ReportingMonth must be between 1 and 12.");
            }

            if (plan.ReportingYear is < MinimumReportingYear or > MaximumReportingYear)
            {
                throw new ApplicationException(
                    $"ReportingYear must be between {MinimumReportingYear} and {MaximumReportingYear}.");
            }

            var measureMappingExists = await _measureMappingRepository.AnyAsync(
                m => m.Id == plan.MeasureMappingId, cancellationToken);

            if (!measureMappingExists)
            {
                throw new ApplicationException($"Measure mapping with Id: {plan.MeasureMappingId} not found.");
            }

            // Facilities are owned by the host service, so existence is confirmed through its API. The
            // check is skipped by configuration (ServiceRegistry:TenantService:CheckIfTenantExists) in
            // deployments that do not want the round trip.
            var facilityExists = await _tenantApiService.CheckFacilityExists(plan.FacilityId, cancellationToken);

            if (!facilityExists)
            {
                throw new ApplicationException($"Facility with Id: {plan.FacilityId} not found.");
            }

            if (await IsDuplicateAsync(plan, currentId, cancellationToken))
            {
                throw new ApplicationException(
                    $"A reporting plan already exists for facility {plan.FacilityId}, measure mapping " +
                    $"{plan.MeasureMappingId} and period {plan.ReportingMonth}/{plan.ReportingYear}.");
            }
        }

        private Task<bool> IsDuplicateAsync(FacilityReportingPlan plan, string? currentId, CancellationToken cancellationToken) =>
            _repository.AnyAsync(p => p.FacilityId == plan.FacilityId
                && p.MeasureMappingId == plan.MeasureMappingId
                && p.ReportingMonth == plan.ReportingMonth
                && p.ReportingYear == plan.ReportingYear
                && (currentId == null || p.Id != currentId), cancellationToken);

        /// <summary>
        /// Decides whether a failed save was the caller's fault. Validation already ruled out the
        /// duplicate and the missing mapping, so reaching here means either another writer won the
        /// race for the unique index - the caller's problem, and a 400 - or the database failed, which
        /// is not, and is left to surface as a server error.
        /// </summary>
        private async Task<Exception> TranslateSaveFailureAsync(FacilityReportingPlan plan, string? currentId,
            DbUpdateException ex, CancellationToken cancellationToken)
        {
            if (await IsDuplicateAsync(plan, currentId, cancellationToken))
            {
                _logger.LogWarning(ex, "Reporting plan for facility {FacilityId} lost a race for the unique period index",
                    plan.FacilityId.SanitizeForLog());

                return new ApplicationException(
                    $"A reporting plan already exists for facility {plan.FacilityId}, measure mapping " +
                    $"{plan.MeasureMappingId} and period {plan.ReportingMonth}/{plan.ReportingYear}.");
            }

            return ex;
        }
    }
}
