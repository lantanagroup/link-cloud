using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Id is required.");

            if (!Guid.TryParse(id, out Guid parsedId))
                return BadRequest("Invalid ID format");

            try
            {
                var reportResource = (await _reportResourceManager.FindAsync(x => x.Id == parsedId)).FirstOrDefault();

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
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var reportResources = await _reportResourceManager.FindAsync(x => x.ReportScheduleId == parsedId);

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
            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return BadRequest("ReportScheduleId is required.");

            if (!Guid.TryParse(reportScheduleId, out Guid parsedId))
                return BadRequest("Invalid ReportScheduleId format");

            try
            {
                var reportResources = await _reportResourceManager.FindAsync(x => x.ReportScheduleId == parsedId && x.PatientId == patientId);

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

        /// <summary>
        /// Returns paged report resources with optional filters
        /// </summary>
        /// <param name="facilityId">Optional facility ID filter</param>
        /// <param name="reportScheduleId">Optional report schedule ID filter</param>
        /// <param name="patientId">Optional patient ID filter</param>
        /// <param name="measureReportId">Optional measure report ID filter</param>
        /// <param name="resourceType">Optional resource type filter</param>
        /// <param name="resourceId">Optional resource ID filter</param>
        /// <param name="sortBy">Optional sort field (e.g., "CreateDate", "ResourceType")</param>
        /// <param name="sortOrder">Optional sort order (Ascending or Descending)</param>
        /// <param name="pageSize">Number of records per page (default: 10)</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ReportResource>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ReportResource>>> Search(
            string? facilityId = null,
            string? reportScheduleId = null,
            string? patientId = null,
            string? measureReportId = null,
            string? resourceType = null,
            string? resourceId = null,
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

                var result = await _reportResourceManager.SearchAsync(
                    facilityId,
                    parsedScheduleId,
                    patientId,
                    measureReportId,
                    resourceType,
                    resourceId,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber);

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(result.Metadata));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(ReportConstants.LoggingIds.SearchPerformed, "Search"), ex, "An exception occurred while attempting to search Report Resource records");

                throw;
            }
        }
    }
}