using AutoMapper;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Tenant.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class FacilityController : ControllerBase
    {

        private readonly FacilityConfigurationService _facilityConfigurationService;

        private readonly IMapper _mapperModelToDto;

        private readonly IMapper _mapperDtoToModel;

        private readonly ILogger<FacilityController> _logger;

        private readonly ISchedulerFactory _schedulerFactory;


        public FacilityController(ILogger<FacilityController> logger, FacilityConfigurationService facilityConfigurationService, ISchedulerFactory schedulerFactory)
        {

            _facilityConfigurationService = facilityConfigurationService;
            _logger = logger;
            _schedulerFactory = schedulerFactory;

            var configModelToDto = new MapperConfiguration(cfg =>
            { 
                cfg.CreateMap<FacilityConfigModel, FacilityConfigDto>();
                cfg.CreateMap<PagedConfigModel<FacilityConfigModel>, PagedFacilityConfigDto>();
                cfg.CreateMap<ScheduledTaskModel.ReportTypeSchedule, ScheduledTaskDto.ReportTypeDtoSchedule>();
                cfg.CreateMap<ScheduledTaskModel, ScheduledTaskDto>();
                cfg.CreateMap<MonthlyReportingPlanModel, MonthlyReportingPlanDto>();
            });

            var configDtoToModel = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<FacilityConfigDto, FacilityConfigModel>();
                cfg.CreateMap<PagedFacilityConfigDto, PagedConfigModel<FacilityConfigModel>>();
                cfg.CreateMap<ScheduledTaskDto.ReportTypeDtoSchedule, ScheduledTaskModel.ReportTypeSchedule>();
                cfg.CreateMap<ScheduledTaskDto, ScheduledTaskModel>();
                cfg.CreateMap<MonthlyReportingPlanDto, MonthlyReportingPlanModel>();
            });

            _mapperModelToDto = configModelToDto.CreateMapper();
            _mapperDtoToModel = configDtoToModel.CreateMapper();
        }

        /// <summary>
        /// Get facilities
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="facilityId"></param>
        /// <param name="facilityName"></param>
        /// <param name="sortBy"></param>
        /// <param name="sortOrder"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityConfigModel>))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilities")]
        public async Task<ActionResult<PagedConfigModel<FacilityConfigModel>>> GetFacilities(string? facilityId, string? facilityName, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            List<FacilityConfigDto> facilitiesDtos;
            PagedFacilityConfigDto pagedFacilityConfigModelDto = new PagedFacilityConfigDto();
            _logger.LogInformation($"Get Facilities");

            if (pageNumber < 1) { pageNumber = 1; }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities");

            PagedConfigModel<FacilityConfigModel> pagedFacilityConfigModel = await _facilityConfigurationService.GetFacilities(facilityId, facilityName, sortBy, sortOrder, pageSize, pageNumber, cancellationToken);

            using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
            {
                facilitiesDtos = _mapperModelToDto.Map<List<FacilityConfigModel>, List<FacilityConfigDto>>(pagedFacilityConfigModel.Records);
                pagedFacilityConfigModelDto.Records = facilitiesDtos;
                pagedFacilityConfigModelDto.Metadata = pagedFacilityConfigModel.Metadata;
            }
            if (pagedFacilityConfigModelDto.Records.Count == 0)
            {
                _logger.LogError("No Facilities Found");

                return NoContent();
            }
            return Ok(pagedFacilityConfigModelDto);
        }

        /// <summary>
        /// Creates a facility configuration.
        /// </summary>
        /// <param name="newFacility"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityConfigDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                await ScheduleService.AddJobsForFacility(facilityConfigModel, scheduler);
            }

            return CreatedAtAction(nameof(StoreFacility), new { id = facilityConfigModel.Id }, facilityConfigModel);
        }

        /// <summary>
        /// Find a facility config by Id
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityConfigDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

            return Ok(dest);
        }


        /// <summary>
        /// Update a facility config.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updatedFacility"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityConfigDto))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFacility(string id, FacilityConfigDto updatedFacility, CancellationToken cancellationToken)
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
                _logger.LogError("Exception is: " + ex.Message);

                throw;
            }

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            // if existingFacility is not null, then update the jobs, else add new jobs
            if (existingFacility != null)
            {
                using (ServiceActivitySource.Instance.StartActivity("Update Jobs for Facility"))
                {
                    await ScheduleService.UpdateJobsForFacility(dest, existingFacility, scheduler);
                }
            }
            else
            {
                using (ServiceActivitySource.Instance.StartActivity("Create Jobs for Facility"))
                {
                    await ScheduleService.AddJobsForFacility(dest, scheduler);
                }
            }

            if (existingFacility == null)
            {
                return CreatedAtAction(nameof(StoreFacility), new { id = dest.Id }, dest);
            }

            return NoContent();
        }

        /// <summary>
        /// Delete a facility by Id.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                _logger.LogError("Exception: " + ex.Message);

                return BadRequest(ex.Message);
            }

            using (ServiceActivitySource.Instance.StartActivity("Delete Jobs for Facility"))
            {
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                await ScheduleService.DeleteJobsForFacility(existingFacility.Id.ToString(), existingFacility.ScheduledTasks, scheduler);
            }

            return NoContent();
        }

    }
}