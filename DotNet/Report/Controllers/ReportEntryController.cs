using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/entries")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportEntryController : ControllerBase
    {
        private readonly ILogger<ReportEntryController> _logger;
        private readonly IDatabase _database;
        private readonly IReportEntryManager _reportEntryManager;
        private readonly IReportScheduledManager _reportScheduledManager;

        public ReportEntryController(ILogger<ReportEntryController> logger, IDatabase database, IReportEntryManager reportEntryManager, IReportScheduledManager reportScheduledManager)
        {
            _logger = logger;
            _database = database;
            _reportEntryManager = reportEntryManager;
            _reportScheduledManager = reportScheduledManager;
        }

        /// <summary>
        /// Returns a report entry record for the given entry Id
        /// </summary>
        /// <param name="id"></param>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportEntry>> GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Id is required.");

            if (!Guid.TryParse(id, out Guid parsedId))
                return BadRequest("Invalid ID format");

            try
            {
                var reportEntry = (await _reportEntryManager.FindAsync(x => x.Id == parsedId)).FirstOrDefault();

                if (reportEntry == null)
                {
                    return NotFound();
                }

                return Ok(reportEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetById"), ex, "An exception occurred while attempting to get a Report Entry record for Id {id}", parsedId);

                throw;
            }
        }

        /// <summary>
        /// Returns report entries for the given entry report schedule Id.
        /// </summary>
        /// <param name="reportScheduleId"></param>
        [HttpGet("schedules/{reportScheduleId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportEntry>>> GetByReportScheduleId(string reportScheduleId)
        {
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var schedule = await _reportScheduledManager.SingleOrDefaultAsync(x => x.Id == parsedId);
                if (schedule == null || schedule.IsDeleted == true)
                    return NotFound();

                var reportEntries = await _reportEntryManager.FindAsync(x => x.ReportScheduleId == parsedId);

                if (reportEntries == null || reportEntries.Count == 0)
                {
                    return NotFound();
                }

                return Ok(reportEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleId"), ex, "An exception occurred while attempting to get a Report Entry record for Report Schedule Id {id}", parsedId);

                throw;
            }
        }

        /// <summary>
        /// Returns a report entry record for the given entry report schedule Id and patient Id.
        /// </summary>
        /// <param name="reportScheduleId"></param>
        /// <param name="patientId"></param>
        [HttpGet("schedules/{reportScheduleId}/patients/{patientId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportEntry>> GetByReportScheduleIdAndPatientId(string reportScheduleId, string patientId)
        {
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var schedule = await _reportScheduledManager.SingleOrDefaultAsync(x => x.Id == parsedId);
                if (schedule == null || schedule.IsDeleted == true)
                    return NotFound();

                var reportEntry = (await _reportEntryManager.FindAsync(x => x.ReportScheduleId == parsedId && x.PatientId == patientId)).FirstOrDefault();

                if (reportEntry == null)
                {
                    return NotFound();
                }

                return Ok(reportEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByReportScheduleIdAndPatientId"), ex, "An exception occurred while attempting to get a Report Entry record for Report Schedule Id {id}, Patient Id {patientId}", parsedId, HtmlInputSanitizer.Sanitize(patientId));

                throw;
            }
        }

        /// <summary>
        /// Returns the count of report entries for the given report schedule Id.
        /// </summary>
        /// <param name="reportScheduleId"></param>
        [HttpGet("schedules/{reportScheduleId}/count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<int>> GetCountByReportScheduleId(string reportScheduleId)
        {
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var schedule = await _reportScheduledManager.SingleOrDefaultAsync(x => x.Id == parsedId);
                if (schedule == null || schedule.IsDeleted == true)
                    return NotFound();

                var count = await _reportEntryManager.CountAsync(x => x.ReportScheduleId == parsedId);

                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetCountByReportScheduleId"), ex, "An exception occurred while attempting to get the count of Report Entry records for Report Schedule Id {id}", parsedId);

                throw;
            }
        }

        /// <summary>
        /// Returns a summary count of report types, reporting statuses, and submission statuses for the given report schedule Id.
        /// </summary>
        /// <param name="reportScheduleId"></param>
        [HttpGet("schedules/{reportScheduleId}/summary")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntrySummary))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportEntrySummary>> GetSummaryByReportScheduleId(string reportScheduleId)
        {
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var schedule = await _reportScheduledManager.SingleOrDefaultAsync(x => x.Id == parsedId);
                if (schedule == null || schedule.IsDeleted == true)
                    return NotFound();

                var summary = await _reportEntryManager.GetSummaryByReportScheduleIdAsync(parsedId);

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetSummaryByReportScheduleId"), ex, "An exception occurred while attempting to get the summary of Report Entry records for Report Schedule Id {id}", parsedId);

                throw;
            }
        }

        /// <summary>
        /// Returns report entry records for the given patient Id. 
        /// </summary>
        /// <param name="patientId"></param>
        [HttpGet("patients/{patientId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportEntry))]
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

        /// <summary>
        /// Returns paged report entries with optional filters
        /// </summary>
        /// <param name="facilityId">Optional facility ID filter</param>
        /// <param name="patientId">Optional patient ID filter</param>
        /// <param name="reportScheduleId">Optional report schedule ID filter</param>
        /// <param name="reportingStatus">Optional reporting status filter</param>
        /// <param name="submissionStatus">Optional submission status filter</param>
        /// <param name="submissionStatusIsNull">Optional flag to filter entries with no submission status</param>
        /// <param name="reportType">Optional report type filter</param>
        /// <param name="sortBy">Optional sort field (e.g., "CreateDate", "PatientId")</param>
        /// <param name="sortOrder">Optional sort order (Ascending or Descending)</param>
        /// <param name="pageSize">Number of records per page (default: 10)</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ReportEntry>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ReportEntry>>> Search(
            string? facilityId = null,
            string? patientId = null,
            string? reportScheduleId = null,
            [FromQuery] List<ReportingStatus>? reportingStatuses = null,
            [FromQuery] List<SubmissionStatus>? submissionStatuses = null,
            bool submissionStatusIsNull = false,
            string? reportType = null,
            string? sortBy = null,
            SortOrder? sortOrder = null,
            int pageSize = 10,
            int pageNumber = 1)
        {
            Guid? parsedScheduleId = null;
            if (!string.IsNullOrWhiteSpace(reportScheduleId) && Guid.TryParse(reportScheduleId, out Guid temp))
                parsedScheduleId = temp;

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

                var result = await _reportEntryManager.SearchAsync(
                    facilityId,
                    patientId,
                    parsedScheduleId,
                    reportingStatuses,
                    submissionStatuses,
                    submissionStatusIsNull,
                    reportType,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber);

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(result.Metadata));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.SearchPerformed, "Search"), ex, "An exception occurred while attempting to search Report Entry records");

                throw;
            }
        }
    }
}