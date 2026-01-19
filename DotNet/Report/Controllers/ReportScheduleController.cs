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
    [Route("api/schedules")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportScheduleController : ControllerBase
    {
        private readonly ILogger<ReportScheduleController> _logger;
        private readonly IDatabase _database;
        private readonly IReportScheduledManager _reportScheduledManager;

        public ReportScheduleController(ILogger<ReportScheduleController> logger, IDatabase database, IReportScheduledManager reportScheduledManager)
        {
            _logger = logger;
            _database = database;
            _reportScheduledManager = reportScheduledManager;
        }

        /// <summary>
        /// Returns a scheduled report record for the given report schedule Id
        /// </summary>
        /// <param name="id"></param>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportSchedule))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportSchedule>> GetById(string id)
        {
            try
            {
                var reportSchedule = (await _reportScheduledManager.FindAsync(x => x.Id == id)).FirstOrDefault();

                if (reportSchedule == null)
                {
                    return NotFound();
                }

                return Ok(reportSchedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetById"), ex, "An exception occurred while attempting to get a Report Schedule record for Id {id}", HtmlInputSanitizer.Sanitize(id));

                throw;
            }
        }

        /// <summary>
        /// Returns scheduled reports for the given facility Id. An optional 'active' parameter is available to only return current active reports.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="active"></param>
        [HttpGet("facilities/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportSchedule))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportSchedule>>> GetByFacilityId(string facilityId, bool? active)
        {
            try
            {
                List<ReportSchedule>? reportSchedules = null;

                if (active == true)
                {
                    reportSchedules = await _reportScheduledManager.FindAsync(x => x.FacilityId == facilityId && x.Status != Shared.Application.Enums.ScheduleStatus.Submitted);
                }
                else 
                {
                    reportSchedules = await _reportScheduledManager.FindAsync(x => x.FacilityId == facilityId);
                }

                if (reportSchedules == null)
                {
                    return NotFound();
                }

                return Ok(reportSchedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByFacilityId"), ex, "An exception occurred while attempting to get a Report Schedule record for Facility Id {id}", HtmlInputSanitizer.Sanitize(facilityId));

                throw;
            }
        }
    }
}
