using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
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
    [Route("api/schedules")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportScheduleController : ControllerBase
    {
        private readonly ILogger<ReportScheduleController> _logger;
        private readonly IDatabase _database;
        private readonly IReportScheduledManager _reportScheduledManager;

        public ReportScheduleController(ILogger<ReportScheduleController> logger, IDatabase database,
            IReportScheduledManager reportScheduledManager)
        {
            _logger = logger;
            _database = database;
            _reportScheduledManager = reportScheduledManager;
        }

        /// <summary>
        /// Returns a scheduled report record for the given report schedule Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="includeDeleted">
        /// When set to <c>true</c>, includes soft-deleted report schedules.
        /// Defaults to <c>false</c>.
        /// </param>
        [HttpGet("{id}")]
        public async Task<ActionResult<ReportScheduleApiModel>> GetById(
            string id,
            [FromQuery] bool includeDeleted = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Id is required.");

            if (!Guid.TryParse(id, out Guid parsedId))
                return BadRequest("Invalid ID format");

            try
            {
                var reportSchedule = (await _reportScheduledManager
                        .FindAsync(x => x.Id == parsedId &&
                                        (includeDeleted || !x.IsDeleted.HasValue || x.IsDeleted == false)))
                    .FirstOrDefault();

                if (reportSchedule == null)
                    return NotFound();

                return Ok(reportSchedule.ToApiModel());
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetById"), ex, "An exception occurred while attempting to get a Report Schedule record for Id {id}", HtmlInputSanitizer.Sanitize(id));

                throw;
            }
        }

        /// <summary>
        /// Returns scheduled reports for the given facility Id.
        /// An optional 'active' parameter is available to only return current active reports.
        /// An optional 'blocking' parameter filters to only reports with statuses that block facility deletion (New, EndOfPeriod).
        /// An optional 'includeDeleted' parameter is available to include soft-deleted reports.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="active"></param>
        /// <param name="blocking">
        /// When set to <c>true</c>, returns only report schedules with a status of <c>New</c> or <c>EndOfPeriod</c>
        /// � the statuses that indicate a report is actively in progress and would block a facility soft-delete.
        /// Defaults to <c>false</c>.
        /// </param>
        /// <param name="includeDeleted">
        /// When set to <c>true</c>, includes soft-deleted report schedules.
        /// Defaults to <c>false</c>.
        /// </param>
        [HttpGet("facilities/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ReportScheduleApiModel>))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ReportScheduleApiModel>>> GetByFacilityId(
            string facilityId,
            [FromQuery] bool? active = null,
            [FromQuery] bool blocking = false,
            [FromQuery] bool includeDeleted = false)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                return BadRequest("FacilityId is required.");

            try
            {
                List<ReportScheduleModel>? reportSchedules;

                if (blocking)
                {
                    // Only statuses that cannot be safely cleaned up: New and EndOfPeriod.
                    // Scheduled reports are handled by deleting their Quartz jobs; Submitted are terminal.
                    reportSchedules = await _reportScheduledManager.FindAsync(x =>
                        x.FacilityId == facilityId &&
                        (x.Status == Shared.Application.Enums.ScheduleStatus.New ||
                         x.Status == Shared.Application.Enums.ScheduleStatus.EndOfPeriod) &&
                        (includeDeleted || !x.IsDeleted.HasValue || x.IsDeleted == false));
                }
                else if (active == true)
                {
                    reportSchedules = await _reportScheduledManager.FindAsync(x =>
                        x.FacilityId == facilityId &&
                        x.Status != Shared.Application.Enums.ScheduleStatus.Submitted &&
                        (includeDeleted || !x.IsDeleted.HasValue || x.IsDeleted == false));
                }
                else
                {
                    reportSchedules = await _reportScheduledManager.FindAsync(x =>
                        x.FacilityId == facilityId &&
                        (includeDeleted || !x.IsDeleted.HasValue || x.IsDeleted == false));
                }

                if (reportSchedules == null)
                    return NotFound();

                return Ok(reportSchedules.Select(s => s.ToApiModel()).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.GetItem, "GetByFacilityId"), ex, "An exception occurred while attempting to get a Report Schedule record for Facility Id {id}", HtmlInputSanitizer.Sanitize(facilityId));

                throw;
            }
        }

        /// <summary>
        /// Soft deletes a single report schedule by its ID.
        /// </summary>
        /// <param name="id">The ID of the report schedule to soft delete.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SoftDelete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Id is required.");

            if (!Guid.TryParse(id, out Guid parsedId))
                return BadRequest("Invalid ID format.");

            try
            {
                await _reportScheduledManager.SoftDeleteByReportTrackingIdAsync(parsedId, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.UpdateItem, "SoftDelete"), ex, "Failed to soft delete report schedule {Id}", HtmlInputSanitizer.Sanitize(id));
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.UpdateItem, "SoftDelete"), ex, "An exception occurred while attempting to soft delete report schedule {Id}", HtmlInputSanitizer.Sanitize(id));
                throw;
            }
        }

        /// <summary>
        /// Restores a single soft-deleted report schedule by its ID.
        /// </summary>
        /// <param name="id">The ID of the report schedule to restore.</param>
        [HttpPatch("{id}/restore")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Restore(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Id is required.");

            if (!Guid.TryParse(id, out Guid parsedId))
                return BadRequest("Invalid ID format.");

            try
            {
                await _reportScheduledManager.RestoreByReportTrackingIdAsync(parsedId, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.UpdateItem, "Restore"), ex, "An exception occurred while attempting to restore report schedule {Id}", HtmlInputSanitizer.Sanitize(id));
                throw;
            }
        }

        /// <summary>
        /// Sets the deleted status for all report schedules associated with a facility.
        /// </summary>
        /// <param name="facilityId">
        /// The unique identifier of the facility.
        /// </param>
        /// <param name="deleted">
        /// When set to <c>true</c>, marks all report schedules for the facility as deleted.
        /// When set to <c>false</c>, restores all previously deleted report schedules.
        /// </param>
        /// <returns>
        /// Returns <see cref="StatusCodes.Status204NoContent"/> when the operation completes successfully.
        /// </returns>
        [HttpPatch("facility/{facilityId}/status")]
        public async Task<IActionResult> SetReportsDeletedStatusForFacility(
            string facilityId,
            [FromQuery] bool deleted)
        {
            await _reportScheduledManager.UpdateReportsDeletedStatusForFacility(facilityId, deleted);
            return NoContent();
        }

        /// <summary>
        /// Returns paged scheduled reports with optional filters
        /// </summary>
        /// <param name="facilityId">Optional facility ID filter</param>
        /// <param name="frequency">Optional frequency filter</param>
        /// <param name="reportType">Optional report type filter</param>
        /// <param name="reportStartDate">Optional report start date filter (inclusive)</param>
        /// <param name="reportEndDate">Optional report end date filter (inclusive)</param>
        /// <param name="status">Optional status filter � supports multiple values (e.g. status=New&amp;status=Submitted)</param>
        /// <param name="endOfReportPeriodJobHasRun">Optional end of report period job flag filter</param>
        /// <param name="includeDeleted">Optional include deleted filter</param>
        /// <param name="sortBy">Optional sort field (e.g., "CreateDate", "ReportStartDate")</param>
        /// <param name="sortOrder">Optional sort order (Ascending or Descending)</param>
        /// <param name="pageSize">Number of records per page (default: 10)</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ReportScheduleApiModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ReportScheduleApiModel>>> Search(
            string? facilityId = null,
            Frequency? frequency = null,
            string? reportType = null,
            DateTime? reportStartDate = null,
            DateTime? reportEndDate = null,
            [FromQuery] List<ScheduleStatus>? status = null,
            bool? endOfReportPeriodJobHasRun = null,
            bool includeDeleted = false,
            string? sortBy = null,
            SortOrder? sortOrder = null,
            int pageSize = 10,
            int pageNumber = 1,
            DateOnly? createDate = null,
            string? id = null)
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

                Guid? parsedId = null;
                if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var guidId))
                    parsedId = guidId;

                var result = await _reportScheduledManager.SearchAsync(
                    facilityId,
                    frequency,
                    reportType,
                    reportStartDate,
                    reportEndDate,
                    statuses: status?.ToArray(),
                    endOfReportPeriodJobHasRun,
                    includeDeleted,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber,
                    cancellationToken: HttpContext.RequestAborted,
                    createDate: createDate,
                    id: parsedId);

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(result.Metadata));

                var apiResult = new PagedConfigModel<ReportScheduleApiModel>(
                    result.Records.Select(s => s.ToApiModel()).ToList(),
                    result.Metadata);

                return Ok(apiResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.SearchPerformed, "Search"), ex,
                    "An exception occurred while attempting to search Report Schedule records");

                throw;
            }
        }

        [HttpGet("summaries")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ReportSummaryApiModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ReportSummaryApiModel>>> GetReportSummaries(
            string? facilityId = null,
            ReportStatus? status = null,
            string? sortBy = null,
            SortOrder? sortOrder = null,
            int pageSize = 10,
            int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            if (pageSize < 1 || pageSize > 100)
                pageSize = 10;

            if (pageNumber < 1)
                pageNumber = 1;

            facilityId = facilityId is null ? null : HtmlInputSanitizer.Sanitize(facilityId);
            sortBy = sortBy is null ? null : HtmlInputSanitizer.Sanitize(sortBy);

            var result = await _reportScheduledManager.GetReportSummaries(
                facilityId,
                status,
                sortBy,
                sortOrder,
                pageSize,
                pageNumber,
                cancellationToken);

            Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(result.Metadata));
            return Ok(result);
        }

        [HttpGet("{reportScheduleId}/summary")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportSummaryApiModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportSummaryApiModel>> GetReportSummary(
            string reportScheduleId,
            CancellationToken cancellationToken = default)
        {
            var result = await _reportScheduledManager.GetReportSummary(
                reportScheduleId,
                cancellationToken);

            return result == null ? NotFound() : Ok(result);
        }
    }
}