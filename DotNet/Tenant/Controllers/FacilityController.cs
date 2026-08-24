using Confluent.Kafka;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Models;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Models;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace LantanaGroup.Link.Tenant.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class FacilityController : ControllerBase
    {
        private readonly IFacilityManager _facilityManager;
        private readonly IFacilityQueries _facilityQueries;

        /// <summary>
        /// The facility operations that change state. Resolved rather than called directly on the
        /// manager so the DMRP module can decorate them when it is enabled.
        /// </summary>
        private readonly IFacilityOperations _facilityOperations;

        private readonly ILogger<FacilityController> _logger;

        private readonly IKafkaProducerFactory<string, GenerateReportValue> _adHocKafkaProducerFactory;
        private readonly IHttpClientFactory _httpClient;
        private readonly ServiceRegistry _serviceRegistry;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ICreateSystemToken _createSystemToken;
        private readonly IOptions<LinkBearerServiceOptions> _linkBearerServiceOptions;

        public FacilityController(ILogger<FacilityController> logger,
            IFacilityManager facilityManager,
            IFacilityQueries facilityQueries,
            IFacilityOperations facilityOperations,
            IKafkaProducerFactory<string, GenerateReportValue> adHocKafkaProducerFactory,
            IOptions<ServiceRegistry> serviceRegistry,
            IHttpClientFactory httpClient,
            IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
            ICreateSystemToken createSystemToken,
            IOptions<LinkBearerServiceOptions> linkBearerServiceOptions)
        {
            _facilityManager = facilityManager;
            _facilityQueries = facilityQueries;
            _facilityOperations = facilityOperations ?? throw new ArgumentNullException(nameof(facilityOperations));
            _logger = logger;

            _adHocKafkaProducerFactory = adHocKafkaProducerFactory;
            _serviceRegistry = serviceRegistry?.Value ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _httpClient = httpClient;
            _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
            _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
            _linkBearerServiceOptions = linkBearerServiceOptions ?? throw new ArgumentNullException(nameof(linkBearerServiceOptions));
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
        /// <param name="includeDeleted"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityModel>))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilities")]
        public async Task<ActionResult<PagedConfigModel<FacilityModel>>> GetFacilities(string? facilityId,
            string? facilityName, string? timeZone, VendorModel? vendor, string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, bool includeDeleted = false,
            CancellationToken cancellationToken = default)
        {
            facilityId = facilityId?.Sanitize();
            facilityName = facilityName?.Sanitize();
            timeZone = timeZone?.Sanitize();
            sortBy = sortBy?.Sanitize();

            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            if (string.IsNullOrEmpty(facilityId) && string.IsNullOrEmpty(facilityName))
            {
                sortBy = "FacilityId";
            }

            sortOrder ??= SortOrder.Ascending;

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities");

            var searchModel = new FacilitySearchModel
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = timeZone,
                Vendor = vendor
            };
            var pagedFacilityConfigModelDto = await _facilityQueries.PagedSearchAsync(searchModel, sortBy, sortOrder.Value, pageSize, pageNumber, includeDeleted, cancellationToken);

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
        public async Task<IActionResult> GetFacilityList([FromQuery] string? search, bool includeDeleted = false)
        {
            try
            {
                FacilitySearchModel searchModel = new FacilitySearchModel();
                if (!string.IsNullOrEmpty(search))
                {
                    searchModel.FacilityName = search;
                    searchModel.FacilityNameContains = true;
                    if (!includeDeleted)
                    {
                        searchModel.IsDeleted = false;
                    }
                }

                var facilities = await _facilityQueries.SearchAsync(searchModel, HttpContext.RequestAborted, includeDeleted);

                if ((facilities?.Count ?? 0) == 0)
                {
                    return NoContent();
                }

                var facilityList = facilities
                    .Where(f => f.FacilityName != null)
                    .ToDictionary(f => f.FacilityId, f => f.FacilityName);

                return Ok(facilityList);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.AddException(ex);
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
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> StoreFacility(FacilityModel newFacility, CancellationToken cancellationToken)
        {
            if (newFacility == null)
            {
                return BadRequest();
            }

            if (newFacility.FacilityName == null)
            {
                return BadRequest();
            }

            if (newFacility.FacilityId == null)
            {
                return BadRequest();
            }

            try
            {
                await _facilityOperations.CreateAsync(newFacility, cancellationToken);
            }
            catch (ScheduledReportsNotAcceptedException ex)
            {
                return BadRequest(ex.Message);
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

            var facilityConfigDto = await _facilityQueries.GetAsync(newFacility.FacilityId, null, cancellationToken);

            return Created($"/api/Facility/{facilityConfigDto.FacilityId}", facilityConfigDto);
        }

        /// <summary>
        /// Gets a facility configuration by facilityId.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{facilityId}")]
        public async Task<IActionResult> GetFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = facilityId?.Sanitize();

            FacilityModel? facilityConfigModel;

            try
            {
                facilityConfigModel = await _facilityQueries.GetAsync(facilityId, null, cancellationToken);
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

            return Ok(facilityConfigModel);
        }

        /// <summary>
        /// Updates a facility configuration.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="facilityConfig"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{facilityId}")]
        public async Task<ActionResult<FacilityModel>> PutFacility(string facilityId, FacilityModel facilityConfig, CancellationToken cancellationToken)
        {
            facilityId = facilityId.Sanitize();

            FacilityModel? existingModel;

            try
            {
                existingModel = await _facilityQueries.GetAsync(facilityId, null, cancellationToken);
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

            if (existingModel == null)
            {
                return NotFound();
            }

            try
            {
                await _facilityOperations.UpdateAsync(existingModel, facilityConfig, cancellationToken);
            }
            catch (ScheduledReportsNotAcceptedException ex)
            {
                return BadRequest(ex.Message);
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

            var facilityConfigDto = await _facilityQueries.GetAsync(facilityId, null, cancellationToken);

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

            var existingModel = await _facilityQueries.GetAsync(facilityId, null, cancellationToken);

            if (existingModel == null)
            {
                return NotFound($"Facility with Id: {facilityId} Not Found");
            }

            try
            {
                await _facilityOperations.DeleteAsync(facilityId, cancellationToken);
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

            return NoContent();
        }

        [HttpDelete("softDelete/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SoftDeleteFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = facilityId?.Sanitize();

            var existingModel = await _facilityQueries.GetAsync(facilityId, null, cancellationToken, includeDeleted: true);
            if (existingModel == null)
                return NotFound($"Facility with Id: {facilityId} Not Found");

            try
            {
                await _facilityOperations.SoftDeleteAsync(facilityId, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityController.SoftDeleteFacility");
                return Problem("An error occurred while soft deleting the facility", null, 500);
            }

            return NoContent();
        }

        /// <summary>
        /// Restores a soft-deleted facility configuration.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("restore/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RestoreFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = facilityId?.Sanitize();

            var existingModel = await _facilityQueries.GetAsync(facilityId, null, cancellationToken, includeDeleted: true);
            if (existingModel == null)
                return NotFound($"Facility with Id: {facilityId} Not Found");

            try
            {
                await _facilityOperations.RestoreAsync(existingModel, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityController.RestoreFacility");
                return Problem("An error occurred while restoring the facility", null, 500);
            }

            return NoContent();
        }

        /// <summary>
        /// Generate
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
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest("FacilityId must be provided.");
            }

            if (await _facilityQueries.GetAsync(facilityId, null, CancellationToken.None) == null)
            {
                return NotFound("Facility does not exist.");
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

            var reportId = Guid.NewGuid();

            try
            {
                foreach (var rt in request.ReportTypes)
                {
                    //this will throw an ApplicationException if the Measure Definition does not exist.
                    await _facilityManager.MeasureDefinitionExists(rt);
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
                        AdhocReportId = reportId,
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

        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GenerateAdhocReportResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("{facilityId}/RegenerateReport")]
        public async Task<ActionResult<GenerateAdhocReportResponse>> RegenerateReport(string facilityId, RegenerateReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest("FacilityId must be provided.");
            }

            if (await _facilityQueries.GetAsync(facilityId, null, CancellationToken.None) == null)
            {
                return NotFound("Facility does not exist.");
            }

            if (string.IsNullOrEmpty(request.ReportId))
            {
                return BadRequest("ReportId must be provided.");
            }

            var reportId = Guid.NewGuid();

            try
            {
                var httpClient = _httpClient.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var baseUrl = new Uri(_serviceRegistry.ReportServiceApiUrl.TrimEnd('/') + "/schedules");

                var requestUrl = $"{baseUrl}/{HtmlInputSanitizer.SanitizeAndRemove(request.ReportId)}";

                if (!_linkBearerServiceOptions.Value.AllowAnonymous)
                {
                    //TODO: add method to get key that includes looking at redis for future use case
                    if (_linkTokenServiceConfig.Value.SigningKey is null) throw new Exception("Link Token Service Signing Key is missing.");

                    var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2);
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await httpClient.GetAsync(requestUrl, cts.Token);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return NotFound($"Report schedule {request.ReportId} not found.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Report Service Call unsuccessful: StatusCode: {response.StatusCode} | Response: {await response.Content.ReadAsStringAsync(CancellationToken.None)} | Query URL: {requestUrl}");
                }

                var producerConfig = new ProducerConfig();

                using var producer = _adHocKafkaProducerFactory.CreateProducer(producerConfig);

                var message = new Message<string, GenerateReportValue>
                {
                    Key = facilityId,
                    Headers = new Headers(),
                    Value = new GenerateReportValue()
                    {
                        ReportId = request.ReportId == null ? null : Guid.Parse(request.ReportId),
                        AdhocReportId = reportId,
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

            return Ok(new GenerateAdhocReportResponse(reportId));
        }
    }
}