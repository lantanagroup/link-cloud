using AutoMapper;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Services;

namespace LantanaGroup.Link.Tenant.Business
{
    /// <summary>
    /// This service's own facility operations: persist the change, then reconcile the facility's
    /// scheduled jobs to it.
    /// </summary>
    /// <remarks>
    /// The endpoints resolve these through <see cref="IFacilityOperations"/> rather than calling the
    /// manager directly, which is what lets the DMRP module wrap them when it is enabled. With the
    /// module off this type is the whole implementation and nothing about the behavior differs from
    /// calling the manager inline.
    /// </remarks>
    public sealed class TenantFacilityOperations : IFacilityOperations
    {
        /// <summary>
        /// Built once rather than per request: the configuration is fixed, and compiling it is the
        /// expensive half of using AutoMapper.
        /// </summary>
        private static readonly IMapper ModelToEntityMapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<FacilityModel, Facility>();
            cfg.CreateMap<TenantScheduledReportConfig, ScheduledReportModel>();
        }).CreateMapper();

        private readonly IFacilityManager _facilityManager;
        private readonly IFacilityQueries _facilityQueries;
        private readonly ScheduleService _scheduleService;

        public TenantFacilityOperations(IFacilityManager facilityManager, IFacilityQueries facilityQueries,
            ScheduleService scheduleService)
        {
            _facilityManager = facilityManager ?? throw new ArgumentNullException(nameof(facilityManager));
            _facilityQueries = facilityQueries ?? throw new ArgumentNullException(nameof(facilityQueries));
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
        }

        public async Task CreateAsync(FacilityModel facility, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(facility);

            var entity = ToEntity(facility);

            await _facilityManager.CreateAsync(entity, cancellationToken);

            using (ServiceActivitySource.Instance.StartActivity("Schedule Jobs for New Facility"))
            {
                await _scheduleService.AddJobsForFacility(entity, cancellationToken);
            }
        }

        public async Task UpdateAsync(FacilityModel existingFacility, FacilityModel updatedFacility,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(existingFacility);
            ArgumentNullException.ThrowIfNull(updatedFacility);

            if (existingFacility.Id is null)
            {
                throw new ArgumentException("The facility being updated has no database id.", nameof(existingFacility));
            }

            var oldEntity = ToEntity(existingFacility);
            var newEntity = ToEntity(updatedFacility);

            await _facilityManager.UpdateAsync(existingFacility.Id.Value, newEntity, cancellationToken);

            using (ServiceActivitySource.Instance.StartActivity("Update Jobs for Facility"))
            {
                await _scheduleService.UpdateJobsForFacility(newEntity, oldEntity, cancellationToken);
            }
        }

        public async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            await _facilityManager.DeleteAsync(facilityId, cancellationToken);

            using (ServiceActivitySource.Instance.StartActivity("Delete Jobs for Facility"))
            {
                await _scheduleService.DeleteJobsForFacility(facilityId, cancellationToken: cancellationToken);
            }
        }

        public async Task SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var existing = await _facilityQueries.GetAsync(facilityId, null, cancellationToken, includeDeleted: true);

            // A facility that is already soft deleted still gets its jobs cleaned up, which self-heals
            // a partial failure from an earlier attempt. DeleteJobsForFacility is a no-op when there
            // are none.
            if (existing?.IsDeleted != true)
            {
                await _facilityManager.SoftDeleteAsync(facilityId, cancellationToken);
            }

            // Jobs are recreated when the facility is restored.
            using (ServiceActivitySource.Instance.StartActivity("Delete Jobs for Facility"))
            {
                await _scheduleService.DeleteJobsForFacility(facilityId, cancellationToken: cancellationToken);
            }
        }

        public async Task RestoreAsync(FacilityModel facility, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(facility);

            await _facilityManager.RestoreAsync(facility.FacilityId!, cancellationToken);

            using (ServiceActivitySource.Instance.StartActivity("Restore Jobs for Facility"))
            {
                await _scheduleService.AddJobsForFacility(ToEntity(facility), cancellationToken);
            }
        }

        private static Facility ToEntity(FacilityModel model) => ModelToEntityMapper.Map<FacilityModel, Facility>(model);
    }
}
