using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.DMRP.Business.Managers
{
    public interface IFacilityReportingPlanManager
    {
        Task<FacilityReportingPlan> CreateAsync(FacilityReportingPlan newFacilityReportingPlan, CancellationToken cancellationToken = default);
        Task UpdateAsync(string id, FacilityReportingPlan facilityReportingPlan, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }

    public class FacilityReportingPlanManager : IFacilityReportingPlanManager
    {
        private readonly ILogger<FacilityReportingPlanManager> _logger;
        private readonly IEntityRepository<FacilityReportingPlan> _repository;

        public FacilityReportingPlanManager(ILogger<FacilityReportingPlanManager> logger, IEntityRepository<FacilityReportingPlan> repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<FacilityReportingPlan> CreateAsync(FacilityReportingPlan newFacilityReportingPlan, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Facility Reporting Plan");

            try
            {
                await _repository.AddAsync(newFacilityReportingPlan, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException("Facility reporting plan failed to create. " + ex.Message);
            }

            return newFacilityReportingPlan;
        }

        public async Task UpdateAsync(string id, FacilityReportingPlan facilityReportingPlan, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Reporting Plan");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Facility reporting plan with Id: {Id} not found", id);
                throw new ApplicationException($"Facility reporting plan with Id: {id} not found");
            }

            try
            {
                _repository.Update(existing);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException($"Facility reporting plan {id} failed to update. " + ex.Message);
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Reporting Plan");

            var existing = await _repository.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                _logger.LogError("Facility reporting plan with Id: {Id} not found", id);
                throw new ApplicationException($"Facility reporting plan with Id: {id} not found");
            }

            try
            {
                _repository.Remove(existing);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw new ApplicationException($"Facility reporting plan {id} failed to delete. " + ex.Message);
            }
        }
    }
}
