using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

        /// <summary>
        /// Returns paged scheduled reports with optional filters
        /// </summary>
        /// <param name="facilityId">Optional facility ID filter</param>
        /// <param name="frequency">Optional frequency filter</param>
        /// <param name="reportType">Optional report type filter</param>
        /// <param name="reportStartDate">Optional report start date filter (inclusive)</param>
        /// <param name="reportEndDate">Optional report end date filter (inclusive)</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="endOfReportPeriodJobHasRun">Optional end of report period job flag filter</param>
        /// <param name="sortBy">Optional sort field (e.g., "CreateDate", "ReportStartDate")</param>
        /// <param name="sortOrder">Optional sort order (Ascending or Descending)</param>
        /// <param name="pageSize">Number of records per page (default: 10)</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ReportSchedule>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ReportSchedule>>> Search(
            string? facilityId = null,
            Frequency? frequency = null,
            string? reportType = null,
            DateTime? reportStartDate = null,
            DateTime? reportEndDate = null,
            ScheduleStatus? status = null,
            bool? endOfReportPeriodJobHasRun = null,
            string? sortBy = null,
            SortOrder? sortOrder = null,
            int pageSize = 10,
            int pageNumber = 1)
        {
            try
            {
                if (pageSize < 1 || pageSize > 100)
                {
                    pageSize = 10;
                }

                if (pageNumber < 1)
                {
                    pageNumber = 1;
                }

                var result = await _reportScheduledManager.SearchAsync(
                    facilityId,
                    frequency,
                    reportType,
                    reportStartDate,
                    reportEndDate,
                    status,
                    endOfReportPeriodJobHasRun,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber);

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(result.Metadata));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.SearchPerformed, "Search"), ex, "An exception occurred while attempting to search Report Schedule records");

                throw;
            }
        }
    }
}
