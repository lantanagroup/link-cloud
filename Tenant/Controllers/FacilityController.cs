using AutoMapper;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Quartz;

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
        public async Task<ActionResult<List<FacilityConfigDto>>> GetAllFacilities( string? facilityId, string? facilityName, CancellationToken cancellationToken)
        {
            var filterBuilder = Builders<FacilityConfigModel>.Filter;
            var filters = new List<FilterDefinition<FacilityConfigModel>>();
            if (!string.IsNullOrEmpty(facilityId))
            {
                filters.Add(filterBuilder.Eq("FacilityId", facilityId));
            }

            if (!string.IsNullOrEmpty(facilityName))
            {
                filters.Add(filterBuilder.Eq("FacilityName", facilityName));
            }
            var finalFilter = filters.Count > 0 ? filterBuilder.And(filters) : null;

            //if no optional filter variables get full list, otherwise filter your search of the list to only return facilities based on the filters
            List<FacilityConfigModel> facilities = finalFilter == null ? await _facilityConfigurationService.GetFacilities(cancellationToken) : _facilityConfigurationService.getFacilityConfigurationRepo().FindAsync(finalFilter, cancellationToken).Result;
            List<FacilityConfigDto> facilitiesDtos = _mapperModelToDto.Map<List<FacilityConfigModel>, List<FacilityConfigDto>>(facilities);
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

            await ScheduleService.AddJobsForFacility(facilityConfigModel, _scheduler);

            return CreatedAtAction(nameof(GetAllFacilities), new { id = facilityConfigModel.Id }, facilityConfigModel);
        }

        /// <summary>
        /// Find a facility config by Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("{facilityId}")]
        public async Task<ActionResult<FacilityConfigDto>> LookupFacilityById(string facilityId, CancellationToken cancellationToken)
        {
            FacilityConfigModel facility = await _facilityConfigurationService.GetFacilityByFacilityId(facilityId,  cancellationToken);

            if (facility is null)
            { 
                _logger.LogError($"Facility with Id: {facilityId} Not Found");

                return NotFound($"Facility with Id: {facilityId} Not Found");
            }

            FacilityConfigDto dest = _mapperModelToDto.Map<FacilityConfigModel, FacilityConfigDto>(facility);

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
        public async Task<IActionResult> UpdateFacility(string id, FacilityConfigDto updatedFacility, CancellationToken cancellationToken)
        {
            FacilityConfigModel dest = _mapperDtoToModel.Map<FacilityConfigDto, FacilityConfigModel>(updatedFacility);

            FacilityConfigModel existingFacility = await _facilityConfigurationService.GetFacilityById(id, cancellationToken);

            // validate id and updatedFacility.id match
            if (id != updatedFacility.Id)
            {
                _logger.LogError( $" {id} in the url and the {updatedFacility.Id} in the payload mismatch");

                return BadRequest($" {id} in the url and the {updatedFacility.Id} in the payload mismatch");
            }
            try
            {
                await _facilityConfigurationService.UpdateFacility(id, dest, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                _logger.LogError( $"Exception: {ex.Message}");

                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                this._logger.LogError("Exception is: " + ex.Message);

                throw;
            }

            await ScheduleService.UpdateJobsForFacility(dest, existingFacility, _scheduler);

            return NoContent();
        }

        /// <summary>
        /// Delete a facility by Id.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpDelete("{facilityId}")]
        public async Task<IActionResult> DeleteFacility(string facilityId, CancellationToken cancellationToken)
        {
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

            await ScheduleService.DeleteJobsForFacility(existingFacility.Id, existingFacility.ScheduledTasks, _scheduler);

            return NoContent();
        }

    }
}