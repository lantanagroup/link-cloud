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
    [Route("api/report-entries")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportEntryController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private readonly IDatabase _database;
        private readonly IReportEntryManager _reportEntryManager;
        private readonly IReportScheduledManager _reportScheduledManager;

        public ReportEntryController(ILogger<ReportController> logger, IDatabase database, IReportEntryManager reportEntryManager, IReportScheduledManager reportScheduledManager)
        {
            _logger = logger;
            _database = database;
            _reportEntryManager = reportEntryManager;
            _reportScheduledManager = reportScheduledManager;
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="id"></param>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportEntry>> GetById(string id)
        {
            try
            {
                var reportEntry = (await _reportEntryManager.FindAsync(x => x.Id == id)).FirstOrDefault();

                if (reportEntry == null)
                {
                    return NotFound();
                }

                return Ok(reportEntry);
            }
            catch (Exception ex) 
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetById"), ex, "An exception occurred while attempting to get a Report Entry record for Id {id}", HtmlInputSanitizer.Sanitize(id));

                throw;
            }
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="reportScheduleId"></param>
        [HttpGet("report-schedules/{reportScheduleId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportEntry>>> GetByReportScheduleId(string reportScheduleId)
        {
            try
            {
                var reportEntries = await _reportEntryManager.FindAsync(x => x.ReportScheduleId == reportScheduleId);

                if (reportEntries == null || reportEntries.Count == 0)
                {
                    return NotFound();
                }

                return Ok(reportEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleId"), ex, "An exception occurred while attempting to get a Report Entry record for Report Schedule Id {id}", HtmlInputSanitizer.Sanitize(reportScheduleId));

                throw;
            }
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="reportScheduleId"></param>
        /// <param name="patientId"></param>
        [HttpGet("report-schedules/{reportScheduleId}/patients/{patientId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportEntry>> GetByReportScheduleIdAndPatientId(string reportScheduleId, string patientId)
        {
            try
            {
                var reportEntry = (await _reportEntryManager.FindAsync(x => x.ReportScheduleId == reportScheduleId && x.PatientId == patientId)).FirstOrDefault();

                if (reportEntry == null)
                {
                    return NotFound();
                }

                return Ok(reportEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleIdAndPatientId"), ex, "An exception occurred while attempting to get a Report Entry record for Report Schedule Id {id}, Patient Id {patientId}", HtmlInputSanitizer.Sanitize(reportScheduleId), HtmlInputSanitizer.Sanitize(patientId));

                throw;
            }
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="patientId"></param>
        [HttpGet("report-schedules/patients/{patientId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportEntry>>> GetByPatientId(string patientId)
        {
            try
            {
                var reportEntries = await _reportEntryManager.FindAsync(x => x.PatientId == patientId);

                if (reportEntries == null || reportEntries.Count == 0)
                {
                    return NotFound();
                }

                return Ok(reportEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByPatientId"), ex, "An exception occurred while attempting to get a Report Entry record for Patient Id {id}", HtmlInputSanitizer.Sanitize(patientId));

                throw;
            }
        }
    }
}
