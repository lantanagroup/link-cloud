using AutoMapper;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Quartz;
using System.Diagnostics;
using System.Net;

namespace LantanaGroup.Link.Tenant.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class FacilityController : ControllerBase
    {
        private readonly IFacilityConfigurationService _facilityConfigurationService;

        private readonly IMapper _mapperModelToDto;

        private readonly IMapper _mapperDtoToModel;

        private readonly ILogger<FacilityController> _logger;

        private readonly ScheduleService _scheduleService;

        private readonly IKafkaProducerFactory<string, GenerateReportValue> _adHocKafkaProducerFactory;

        private readonly IHttpClientFactory _httpClient;
        private readonly ServiceRegistry _serviceRegistry;

        public FacilityController(ILogger<FacilityController> logger,
            IFacilityConfigurationService facilityConfigurationService, ScheduleService scheduleService,
            IKafkaProducerFactory<string, GenerateReportValue> adHocKafkaProducerFactory,
            IOptions<ServiceRegistry> serviceRegistry, IHttpClientFactory httpClient)
        {
            _facilityConfigurationService = facilityConfigurationService;
            _scheduleService = scheduleService;
            _logger = logger;

            var configModelToDto = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Facility, FacilityConfig>();
                cfg.CreateMap<PagedConfigModel<Facility>, PagedFacilityConfigDto>();
                cfg.CreateMap<ScheduledReportModel, TenantScheduledReportConfig>();
            });

            var configDtoToModel = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<FacilityConfig, Facility>();
                cfg.CreateMap<PagedFacilityConfigDto, PagedConfigModel<Facility>>();
                cfg.CreateMap<TenantScheduledReportConfig, ScheduledReportModel>();
            });

            _mapperModelToDto = configModelToDto.CreateMapper();
            _mapperDtoToModel = configDtoToModel.CreateMapper();
            _adHocKafkaProducerFactory = adHocKafkaProducerFactory;
            _serviceRegistry = serviceRegistry?.Value ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _httpClient = httpClient;
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<PagedFacilityConfigDto>))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilities")]
        public async Task<ActionResult<PagedConfigModel<Facility>>> GetFacilities(string? facilityId,
            string? facilityName, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            facilityId = facilityId?.Sanitize();
            facilityName = facilityName?.Sanitize();
            sortBy = sortBy?.Sanitize();

            List<FacilityConfig> facilitiesDtos;
            PagedFacilityConfigDto pagedFacilityConfigModelDto = new PagedFacilityConfigDto();
            _logger.LogInformation($"Get Facilities");

            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities");

            PagedConfigModel<Facility> pagedFacilityConfigModel =
                await _facilityConfigurationService.GetFacilities(facilityId, facilityName, sortBy, sortOrder, pageSize,
                    pageNumber, cancellationToken);

            using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
            {
                facilitiesDtos =
                    _mapperModelToDto.Map<List<Facility>, List<FacilityConfig>>(pagedFacilityConfigModel
                        .Records);
                pagedFacilityConfigModelDto.Records = facilitiesDtos;
                pagedFacilityConfigModelDto.Metadata = pagedFacilityConfigModel.Metadata;
            }

            if (pagedFacilityConfigModelDto.Records.Count == 0)
            {
                return NoContent();
            }

            return Ok(pagedFacilityConfigModelDto);
        }

        /// <summary>
        /// Get a list of all facilities
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Dictionary<string, string>))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("list")]
        public async Task<IActionResult> GetFacilityList([FromQuery] string? search)
        {
            try
            {
                var facilities = await _facilityConfigurationService.GetAllFacilities(HttpContext.RequestAborted);

                if (facilities.Count == 0)
                {
                    return NoContent();
                }

                if (!string.IsNullOrEmpty(search))
                {
                    facilities = facilities
                        .Where(f => f.FacilityName != null && f.FacilityName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var facilityList = facilities
                    .Where(f => f.FacilityName != null)
                    .ToDictionary(f => f.FacilityId, f => f.FacilityName);

                return Ok(facilityList);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.RecordException(ex);
                _logger.LogError(ex, "Exception Encountered in FacilityController.GetFacilityList");
                return Problem("An error occurred while getting all facilities", null, 500);
            }
        }

        /// <summary>
        /// Creates a facility configuration.
        /// </summary>
        /// <param name="newFacility"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> StoreFacility(FacilityConfig newFacility, CancellationToken cancellationToken)
        {
            var facilityConfigModel = _mapperDtoToModel.Map<FacilityConfig, Facility>(newFacility);

            try
            {
                await _facilityConfigurationService.CreateFacility(facilityConfigModel, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Encountered in FacilityController.StoreFacility");
                return Problem("An error occurred while creating the facility", null, 500);
            }

            using (ServiceActivitySource.Instance.StartActivity("Schedule Jobs for New Facility"))
            {
                await _scheduleService.AddJobsForFacility(facilityConfigModel, cancellationToken);
            }

            var facilityConfigDto = _mapperModelToDto.Map<Facility, FacilityConfig>(facilityConfigModel);

            return Created($"/api/Facility/{facilityConfigDto.FacilityId}", facilityConfigDto);
        }

        /// <summary>
        /// Gets a facility configuration by facilityId.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{facilityId}")]
        public async Task<IActionResult> GetFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = facilityId?.Sanitize();

            Facility? facilityConfigModel;

            try
            {
                facilityConfigModel = await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Encountered in FacilityController.GetFacility");
                return Problem("An error occurred while getting the facility", null, 500);
            }

            if (facilityConfigModel == null)
            {
                return NotFound();
            }

            var facilityConfigDto = _mapperModelToDto.Map<Facility, FacilityConfig>(facilityConfigModel);

            return Ok(facilityConfigDto);
        }

        /// <summary>
        /// Updates a facility configuration.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="facilityConfig"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{facilityId}")]
        public async Task<IActionResult> PutFacility(string facilityId, FacilityConfig facilityConfig, CancellationToken cancellationToken)
        {
            facilityId = facilityId?.Sanitize();

            var facility = _mapperDtoToModel.Map<FacilityConfig, Facility>(facilityConfig);

            Facility? existingFacility;

            try
            {
                existingFacility = await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Encountered in FacilityController.PutFacility");
                return Problem("An error occurred while getting the facility", null, 500);
            }

            if (existingFacility == null)
            {
                return NotFound();
            }

            try
            {
                await _facilityConfigurationService.UpdateFacility(facility.Id, facility, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Encountered in FacilityController.PutFacility");
                return Problem("An error occurred while updating the facility", null, 500);
            }

            using (ServiceActivitySource.Instance.StartActivity("Update Jobs for Facility"))
            {
                await _scheduleService.UpdateJobsForFacility(facility, existingFacility, cancellationToken);
            }

            var facilityConfigDto = _mapperModelToDto.Map<Facility, FacilityConfig>(facility);

            return Ok(facilityConfigDto);
        }

        /// <summary>
        /// Deletes a facility configuration.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("{facilityId}")]
        public async Task<IActionResult> DeleteFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = facilityId?.Sanitize();

            var existingFacility = await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, cancellationToken);

            try
            {
                await _facilityConfigurationService.RemoveFacility(facilityId, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Encountered in FacilityController.DeleteFacility");
                return Problem("An error occurred while deleting the facility", null, 500);
            }

            using (ServiceActivitySource.Instance.StartActivity("Delete Jobs for Facility"))
            {
                await _scheduleService.DeleteJobsForFacility(existingFacility.FacilityId, cancellationToken: cancellationToken);
            }

            return NoContent();
        }

        /// <summary>
        /// Generat
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GenerateAdhocReportResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("{facilityId}/AdHocReport")]
        public async Task<ActionResult<GenerateAdhocReportResponse>> GenerateAdHocReport(string facilityId, AdHocReportRequest request)
        {
            if (string.IsNullOrEmpty(facilityId) ||
                await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, CancellationToken.None) == null)
            {
                return BadRequest("Facility does not exist.");
            }

            if (request.ReportTypes == null || request.ReportTypes.Count == 0)
            {
                return BadRequest("ReportTypes must be provided.");
            }

            if (request.StartDate == null || request.StartDate == DateTime.MinValue)
            {
                return BadRequest("StartDate must be provided.");
            }

            if (request.EndDate == null || request.EndDate == DateTime.MinValue)
            {
                return BadRequest("EndDate must be provided.");
            }

            if (request.EndDate <= request.StartDate)
            {
                return BadRequest("EndDate must be after StartDate.");
            }

            var reportId = Guid.NewGuid().ToString();

            try
            {
                foreach (var rt in request.ReportTypes)
                {
                    //this will throw an ApplicationException if the Measure Definition does not exist.
                    await _facilityConfigurationService.MeasureDefinitionExists(rt);
                }

                var producerConfig = new ProducerConfig();

                using var producer = _adHocKafkaProducerFactory.CreateProducer(producerConfig);

                var startDate = new DateTime(
                    request.StartDate.Value.Year,
                    request.StartDate.Value.Month,
                    request.StartDate.Value.Day,
                    request.StartDate.Value.Hour,
                    request.StartDate.Value.Minute,
                    request.StartDate.Value.Second,
                    DateTimeKind.Utc
                );

                var endDate = new DateTime(
                    request.EndDate.Value.Year,
                    request.EndDate.Value.Month,
                    request.EndDate.Value.Day,
                    request.EndDate.Value.Hour,
                    request.EndDate.Value.Minute,
                    request.EndDate.Value.Second,
                    DateTimeKind.Utc
                );

                var message = new Message<string, GenerateReportValue>
                {
                    Key = facilityId,
                    Headers = new Headers(),
                    Value = new GenerateReportValue
                    {
                        ReportId = reportId,
                        StartDate = startDate,
                        EndDate = endDate,
                        ReportTypes = request.ReportTypes,
                        PatientIds = request.PatientIds,
                        BypassSubmission = request.BypassSubmission ?? false
                    },
                };

                await producer.ProduceAsync(KafkaTopic.GenerateReportRequested.ToString(), message,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityController.GenerateAdHocReport");
                return Problem("An internal server error occurred.", statusCode: 500);
            }

            return Ok(new GenerateAdhocReportResponse(reportId));
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("{facilityId}/RegenerateReport")]
        public async Task<IActionResult> RegenerateReport(string facilityId, RegenerateReportRequest request)
        {
            if (string.IsNullOrEmpty(facilityId) ||
                await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, CancellationToken.None) == null)
            {
                return BadRequest("Facility does not exist.");
            }

            if (string.IsNullOrEmpty(request.ReportId))
            {
                return BadRequest("ReportId must be provided.");
            }

            try
            {
                var httpClient = _httpClient.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                string requestUrl =
                    $"{_serviceRegistry.ReportServiceApiUrl.Trim('/')}/Report/Schedule?FacilityId={facilityId}&reportScheduleId={request.ReportId}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await httpClient.GetAsync(requestUrl, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Report Service Call unsuccessful: StatusCode: {response.StatusCode} | Response: {await response.Content.ReadAsStringAsync(CancellationToken.None)} | Query URL: {requestUrl}");
                }

                var reportScheduleSummary =
                    (ReportScheduleSummaryModel?)await response.Content.ReadFromJsonAsync(
                        typeof(ReportScheduleSummaryModel), CancellationToken.None);

                if (reportScheduleSummary == null)
                {
                    return Problem("No ReportSchedule found for the provided ReportScheduleId",
                        statusCode: (int)HttpStatusCode.NotFound);
                }

                var producerConfig = new ProducerConfig();

                using var producer = _adHocKafkaProducerFactory.CreateProducer(producerConfig);

                var message = new Message<string, GenerateReportValue>
                {
                    Key = reportScheduleSummary.FacilityId,
                    Headers = new Headers(),
                    Value = new GenerateReportValue()
                    {
                        ReportId = reportScheduleSummary.ReportId,
                        Regenerate = true,
                        BypassSubmission = request.BypassSubmission ?? false
                    },
                };

                await producer.ProduceAsync(KafkaTopic.GenerateReportRequested.ToString(), message,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityController.RegenerateReport");
                return Problem("An internal server error occurred.", statusCode: 500);
            }

            return Ok();
        }
    }
}