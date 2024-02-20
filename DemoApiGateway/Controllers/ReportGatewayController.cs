using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/report")]
    [ApiController]
    public class ReportGatewayController : ControllerBase
    {

        private readonly ILogger<ReportGatewayController> _logger;
        private readonly IReportService _reportService;



        public ReportGatewayController(ILogger<ReportGatewayController> logger, IReportService reportService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        }


        [HttpGet]
        [Route("config/reports/{facilityId}")]
        [ProducesResponseType(typeof(List<ReportConfigModel>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ListReports(string facilityId)
        {

            try
            {
                var response = await _reportService.GetReports(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    //TODO : add pagination metadata to response tenant service method
                    var result = await response.Content.ReadFromJsonAsync<List<ReportConfigModel>>();

                    if (result == null) { return NotFound(); }

                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to list facilities.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.ListItems, "List reports"), ex, "An exception occurred while attempting to list reports");
                return StatusCode(500, ex);
            }
        }


        // GET api/<ReportGatewayController>/5
        /// <summary>
        /// Get a report configuration
        /// </summary>
        /// param name="reportId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("config/{reportId}")]
        public async Task<IActionResult> Get(string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
            {
                return BadRequest();
            }

            var response = await _reportService.GetReport(reportId);

            try
            {
                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
                {
                    var result = await response.Content.ReadFromJsonAsync<ReportConfigModel>();
                    return Ok(result);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
                {
                    return NotFound();
                }

                return StatusCode(500, "An error occurred while attempting to get a report configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("reportId", reportId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a report configuration"), ex, "An exception occurred while attempting to get a report configuration.");
                return StatusCode(500);
            }
        }

        // POST api/<ReportGatewayController>
        /// <summary>
        /// Create a new report config
        /// </summary>
        /// param name="reportConfig"></param>
        /// <returns>
        ///     200: Success
        ///     401: Unauthorized
        ///     403: Forbidden
        ///     500: Server Error
        ///     400: Bad Request
        /// </returns>

        [HttpPost("config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] ReportConfigModel reportConfig)
        {
            if (reportConfig == null)
            {
                return BadRequest();
            }

            var response = await _reportService.CreateReport(reportConfig);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new Application.models.EntityActionResponse() { Id = "temp", Message = "Report Configuration created." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to create a new report configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("reportConfig", reportConfig);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a report configuration"), ex, "An exception occurred while attempting to create a report configuration.");
                return StatusCode(500);
            }
        }

        // PUT api/<ReportGatewayController>/5
        /// <summary>
        /// Updates a report configuration
        /// </summary>
        ///<param name="reportConfig"></param>
        ///<param name="reportId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("config/{reportId}")]
        public async Task<IActionResult> Update(string reportId, [FromBody] ReportConfigModel reportConfig)
        {

            if (string.IsNullOrWhiteSpace(reportId))
            {
                return BadRequest();
            }

            if (reportId == null)
            {
                return BadRequest();
            }

            var response = await _reportService.UpdateReport(reportId, reportConfig);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = reportId, Message = "Report Configuration updated." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to update a report configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("reportId", reportId);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a report configuration"), ex, "An exception occurred while attempting to update a report configuration.");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Delete a report config
        /// </summary>
        /// <param name="reportId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("config/{reportId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
            {
                return BadRequest();
            }

            var response = await _reportService.DeleteReport(reportId);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Report Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a report configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("reportId", reportId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a report configuration"), ex, "An exception occurred while attempting to delete a report configuration.");
                return StatusCode(500);
            }
        }
    }
}
