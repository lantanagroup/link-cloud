using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/populations")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportPopulationController : ControllerBase
    {
        private readonly ILogger<ReportPopulationController> _logger;
        private readonly IDatabase _database;
        private readonly IReportPopulationManager _reportPopulationManager;

        public ReportPopulationController(ILogger<ReportPopulationController> logger, IDatabase database, IReportPopulationManager reportPopulationManager)
        {
            _logger = logger;
            _database = database;
            _reportPopulationManager = reportPopulationManager;
        }

        /// <summary>
        /// Returns a report population record for the given Id
        /// </summary>
        /// <param name="id"></param>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportPopulation))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportPopulation>> GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Id is required.");

            if (!Guid.TryParse(id, out Guid parsedId))
                return BadRequest("Invalid ID format");

            try
            {
                var reportPopulation = (await _reportPopulationManager.FindAsync(x => x.Id == parsedId)).FirstOrDefault();

                if (reportPopulation == null)
                {
                    return NotFound();
                }

                return Ok(reportPopulation);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetById"), ex, "An exception occurred while attempting to get a Report Population record for Id {id}", HtmlInputSanitizer.Sanitize(id));

                throw;
            }
        }

        /// <summary>
        /// Returns report population records for the given report schedule Id. An optional 'reportType' parameter is available to specify which associated report type the returned population should be for. 
        /// </summary>
        /// <param name="reportScheduleId"></param>
        /// <param name="reportType"></param>
        [HttpGet("schedules/{reportScheduleId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportPopulation))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportPopulation>>> GetByReportScheduleId(string reportScheduleId, string? reportType)
        {
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                List<ReportPopulationModel>? reportPopulation = null;

                if (reportType == null)
                {
                    reportPopulation = (await _reportPopulationManager.FindAsync(x => x.ReportScheduleId == parsedId));
                }
                else
                {
                    reportPopulation = (await _reportPopulationManager.FindAsync(x => x.ReportScheduleId == parsedId && x.ReportType == reportType));
                }

                if (reportPopulation == null)
                {
                    return NotFound();
                }

                return Ok(reportPopulation);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleId"), ex, "An exception occurred while attempting to get a Report Population record for Report Schedule Id {id}", HtmlInputSanitizer.Sanitize(reportScheduleId));

                throw;
            }
        }

        /// <summary>
        /// Returns the count of MeasureReportPopulations in the initial population for the given report schedule Id
        /// </summary>
        /// <param name="reportScheduleId"></param>
        /// <param name="cancellationToken"></param>
        [HttpGet("schedules/{reportScheduleId}/initial-population-count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<int>> GetInitialPopulationCount(string reportScheduleId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var count = await _reportPopulationManager.CountNumberOfMeasureReportPopulationsInIP(parsedId, cancellationToken);

                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetInitialPopulationCount"), ex, "An exception occurred while attempting to count MeasureReportPopulations in initial population for Report Schedule Id {id}", HtmlInputSanitizer.Sanitize(reportScheduleId));

                throw;
            }
        }
    }
}