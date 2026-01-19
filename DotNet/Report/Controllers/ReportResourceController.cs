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
    [Route("api/resources")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportResourceController : ControllerBase
    {
        private readonly ILogger<ReportResourceController> _logger;
        private readonly IDatabase _database;
        private readonly IReportResourceManager _reportResourceManager;

        public ReportResourceController(ILogger<ReportResourceController> logger, IDatabase database, IReportResourceManager reportResourceManager)
        {
            _logger = logger;
            _database = database;
            _reportResourceManager = reportResourceManager;
        }

        /// <summary>
        /// Returns a report resource record for the given report resource Id
        /// </summary>
        /// <param name="id"></param>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportResource))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportResource>> GetById(string id)
        {
            try
            {
                var reportResource = (await _reportResourceManager.FindAsync(x => x.Id == id)).FirstOrDefault();

                if (reportResource == null)
                {
                    return NotFound();
                }

                return Ok(reportResource);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetById"), ex, "An exception occurred while attempting to get a Report Resource record for Id {id}", HtmlInputSanitizer.Sanitize(id));

                throw;
            }
        }

        /// <summary>
        /// Returns report resource entries for the given report schedule Id. 
        /// </summary>
        /// <param name="reportScheduleId"></param>
        [HttpGet("schedules/{reportScheduleId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportResource))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportResource>>> GetByReportScheduleId(string reportScheduleId)
        {
            try
            {
                var reportResources = await _reportResourceManager.FindAsync(x => x.ReportScheduledId == reportScheduleId);

                if (reportResources == null)
                {
                    return NotFound();
                }

                return Ok(reportResources);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleId"), ex, "An exception occurred while attempting to get a Report Resource record for Report Schedule Id {id}", HtmlInputSanitizer.Sanitize(reportScheduleId));

                throw;
            }
        }

        /// <summary>
        /// Returns report resource records for the given report schedule Id and patient Id.
        /// </summary>
        /// <param name="reportScheduleId"></param>
        /// <param name="patientId"></param>
        [HttpGet("schedules/{reportScheduleId}/patients/{patientId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportResource))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportResource>>> GetByReportScheduleIdAndPatientId(string reportScheduleId, string patientId)
        {
            try
            {
                var reportResources = await _reportResourceManager.FindAsync(x => x.ReportScheduledId == reportScheduleId && x.PatientId == patientId);

                if (reportResources == null)
                {
                    return NotFound();
                }

                return Ok(reportResources);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleIdAndPatientId"), ex, "An exception occurred while attempting to get a Report Resource record for Report Schedule Id {id} and Patient Id {patientId}", HtmlInputSanitizer.Sanitize(reportScheduleId), HtmlInputSanitizer.Sanitize(patientId));

                throw;
            }
        }

        /// <summary>
        /// Returns report resource records for the given patient Id. 
        /// </summary>
        /// <param name="patientId"></param>
        [HttpGet("patients/{patientId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportResource))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportResource>>> GetByPatientId(string patientId)
        {
            try
            {
                var reportResources = await _reportResourceManager.FindAsync(x => x.PatientId == patientId);

                if (reportResources == null)
                {
                    return NotFound();
                }

                return Ok(reportResources);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByPatientId"), ex, "An exception occurred while attempting to get a Report Resource record for Patient Id {id}", HtmlInputSanitizer.Sanitize(patientId));

                throw;
            }
        }
    }
}
