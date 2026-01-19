using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
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
        /// TODO
        /// </summary>
        /// <param name="id"></param>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportPopulation))]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportPopulation>> GetById(string id)
        {
            try
            {
                var reportPopulation = (await _reportPopulationManager.FindAsync(x => x.Id == id)).FirstOrDefault();

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
        /// TODO
        /// </summary>
        /// <param name="reportScheduleId"></param>
        /// <param name="reportType"></param>
        [HttpGet("schedules/{reportScheduleId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportPopulation))]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportPopulation>>> GetByReportScheduleId(string reportScheduleId, string? reportType)
        {
            try
            {
                List<ReportPopulation>? reportPopulation = null;

                if (reportType == null)
                {
                    reportPopulation = (await _reportPopulationManager.FindAsync(x => x.ReportScheduleId == reportScheduleId));
                }
                else 
                {
                    reportPopulation = (await _reportPopulationManager.FindAsync(x => x.ReportScheduleId == reportScheduleId && x.ReportType == reportType));
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
    }
}
