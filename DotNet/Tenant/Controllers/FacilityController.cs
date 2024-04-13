using AutoMapper;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using System.Diagnostics;

namespace LantanaGroup.Link.Tenant.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FacilityController : ControllerBase
    {

        private readonly FacilityConfigurationService _facilityConfigurationService;

        private readonly IMapper _mapperModelToDto;

        private readonly IMapper _mapperDtoToModel;

        private readonly ILogger<FacilityController> _logger;

        private readonly ISchedulerFactory _schedulerFactory;
        public IScheduler _scheduler { get; set; }


        public FacilityController(ILogger<FacilityController> logger, FacilityConfigurationService facilityConfigurationService, ISchedulerFactory schedulerFactory)
        {

            _facilityConfigurationService = facilityConfigurationService;
            _schedulerFactory = schedulerFactory;
            _logger = logger;
            _scheduler = _schedulerFactory.GetScheduler().Result;

            var configModelToDto = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<FacilityConfigModel, FacilityConfigDto>();
                cfg.CreateMap<ScheduledTaskModel.ReportTypeSchedule, ScheduledTaskDto.ReportTypeDtoSchedule>();
                cfg.CreateMap<ScheduledTaskModel, ScheduledTaskDto>();
                cfg.CreateMap<MonthlyReportingPlanModel, MonthlyReportingPlanDto>();
            });

            var configDtoToModel = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<FacilityConfigDto, FacilityConfigModel>();
                cfg.CreateMap<ScheduledTaskDto.ReportTypeDtoSchedule, ScheduledTaskModel.ReportTypeSchedule>();
                cfg.CreateMap<ScheduledTaskDto, ScheduledTaskModel>();
                cfg.CreateMap<MonthlyReportingPlanDto, MonthlyReportingPlanModel>();
            });

            _mapperModelToDto = configModelToDto.CreateMapper();
            _mapperDtoToModel = configDtoToModel.CreateMapper();
        }

        /// <summary>
        /// Get all facilities
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="facilityId"></param>
        /// <param name="facilityName"></param>
        /// <returns></returns>
        [HttpGet(Name = "GetAllFacilities")]
        public async Task<ActionResult<List<FacilityConfigDto>>> GetAllFacilities(string? facilityId, string? facilityName, CancellationToken cancellationToken)
        {
            List<FacilityConfigDto> facilitiesDtos;
            List<FacilityConfigModel> facilities;

            _logger.LogInformation($"Get Facilities");

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities");

            if (facilityId == null && facilityName == null)
            {
                facilities = await _facilityConfigurationService.GetAllFacilities(cancellationToken);
            }
            else
            {
                facilities = await _facilityConfigurationService.GetFacilitiesByFilters(facilityId, facilityName, cancellationToken);
            }

            using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
            {
                facilitiesDtos = _mapperModelToDto.Map<List<FacilityConfigModel>, List<FacilityConfigDto>>(facilities);
            }
            return Ok(facilitiesDtos);
        }

        /// <summary>
        /// Creates a facility configuration.
        /// </summary>
        /// <param name="newFacility"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> StoreFacility(FacilityConfigDto newFacility, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Store Facility with Id: {newFacility.Id} and Facility Name: {newFacility.FacilityName}");

            FacilityConfigModel facilityConfigModel = _mapperDtoToModel.Map<FacilityConfigDto, FacilityConfigModel>(newFacility);

            try
            {
                await _facilityConfigurationService.CreateFacility(facilityConfigModel, cancellationToken);

            }
            catch (ApplicationException ex)
            {
                _logger.LogError($"Store facility exception: {ex.Message}");

                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception is: {ex.Message}");

                throw;
            }

            // create jobs for the new Facility
            using (ServiceActivitySource.Instance.StartActivity("Add Jobs for Facility"))
            {
                await ScheduleService.AddJobsForFacility(facilityConfigModel, _scheduler);
            }

            return CreatedAtAction(nameof(GetAllFacilities), new { id = facilityConfigModel.Id }, facilityConfigModel);
        }

        /// <summary>
        /// Find a facility config by Id
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("{facilityId}")]
        public async Task<ActionResult<FacilityConfigDto>> LookupFacilityById(string facilityId, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Get Facility with Facility Id: {facilityId} ");

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility By Facility Id");

            FacilityConfigModel facility = await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, cancellationToken);

            if (facility is null)
            {
                _logger.LogError($"Facility with Id: {facilityId} Not Found");

                return NotFound($"Facility with Id: {facilityId} Not Found");
            }
            FacilityConfigDto? dest = null;

            using (ServiceActivitySource.Instance.StartActivity("Map Result"))
            {
                dest = _mapperModelToDto.Map<FacilityConfigModel, FacilityConfigDto>(facility);
            }

            return this.Ok(dest);
        }


        /// <summary>
        /// Update a facility config.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updatedFacility"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFacility(Guid id, FacilityConfigDto updatedFacility, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Update Facility with Id: {updatedFacility.Id} and Facility Name: {updatedFacility.FacilityName}");

            FacilityConfigModel dest = _mapperDtoToModel.Map<FacilityConfigDto, FacilityConfigModel>(updatedFacility);

            FacilityConfigModel existingFacility = await _facilityConfigurationService.GetFacilityById(id, cancellationToken);

            // validate id and updatedFacility.id match
            if (id.ToString() != updatedFacility.Id)
            {
                _logger.LogError($" {id} in the url and the {updatedFacility.Id} in the payload mismatch");

                return BadRequest($" {id} in the url and the {updatedFacility.Id} in the payload mismatch");
            }
            try
            {

                await _facilityConfigurationService.UpdateFacility(id, dest, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                _logger.LogError($"Exception: {ex.Message}");

                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                this._logger.LogError("Exception is: " + ex.Message);

                throw;
            }

            using (ServiceActivitySource.Instance.StartActivity("Update Jobs for Facility"))
            {
                await ScheduleService.UpdateJobsForFacility(dest, existingFacility, _scheduler);
            }

            return NoContent();
        }

        /// <summary>
        /// Delete a facility by Id.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpDelete("{facilityId}")]
        public async Task<IActionResult> DeleteFacility(string facilityId, CancellationToken cancellationToken)
        {

            _logger.LogInformation($"Delete Facility with Facility Id: {facilityId}");

            FacilityConfigModel existingFacility = _facilityConfigurationService.GetFacilityByFacilityId(facilityId, cancellationToken).Result;

            try
            {
                await _facilityConfigurationService.RemoveFacility(facilityId, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                this._logger.LogError("Exception: " + ex.Message);

                return BadRequest(ex.Message);
            }

            using (ServiceActivitySource.Instance.StartActivity("Delete Jobs for Facility"))
            {
                await ScheduleService.DeleteJobsForFacility(existingFacility.Id.ToString(), existingFacility.ScheduledTasks, _scheduler);
            }

            return NoContent();
        }

    }
}