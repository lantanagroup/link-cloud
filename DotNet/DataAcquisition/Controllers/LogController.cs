using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Settings;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/acquisition-logs")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class LogController : Controller
{
    private readonly ILogger<LogController> _logger;
    private readonly IDataAcquisitionLogService _logService;
    private readonly IDataAcquisitionLogManager _logManager;
    private readonly IDataAcquisitionLogQueries _logQueries;
    private readonly IDataAcquisitionLogNotesQueries _logNotesQueries;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;

    private const int DefaultCancelMinAgeHours = 24;

    public LogController(
        ILogger<LogController> logger,
        IDataAcquisitionLogService logService,
        IDataAcquisitionLogManager logManager,
        IDataAcquisitionLogQueries queries,
        IDataAcquisitionLogNotesQueries logNotesQueries,
        IReferenceResourcesQueries referenceResourcesQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
        _logQueries = queries ?? throw new ArgumentNullException(nameof(queries));
        _logNotesQueries = logNotesQueries ?? throw new ArgumentNullException(nameof(logNotesQueries));
        _referenceResourcesQueries = referenceResourcesQueries ?? throw new ArgumentNullException(nameof(referenceResourcesQueries));
    }

    /// <summary>
    /// Get a list of data acquisition logs.
    /// </summary>
    /// <remarks>
    /// This endpoint retrieves a list of data acquisition logs.
    /// </remarks>
    /// <param name="queryParameters"></param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of data acquisition logs.</returns>
    /// <response code="200">Returns a list of data acquisition logs.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="404">If no data acquisition logs are found.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IPagedModel<QueryLogSummaryModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<QueryLogSummaryModel>>> Search(
        [FromQuery] LogSearchParameters? queryParameters,
        CancellationToken cancellationToken = default
    )
    {
        if (queryParameters == null)
        {
            return BadRequest("Query parameters are required.");
        }

        if (ModelState.IsValid)
        {
            string facilityId = string.Empty;
            string patientId = string.Empty;
            string reportId = string.Empty;
            string resourceId = string.Empty;

            try
            {
                var allowedSortBy = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "ExecutionDate", "CreateDate", "CompletionDate", "FacilityId", "PatientId", "QueryType", "QueryPhase", "Status", "Priority", "Id", "RetryAttempts", "IsDeleted", "ReportTrackingId" };

                if (!allowedSortBy.Contains(queryParameters.SortBy))
                {
                    return BadRequest($"Invalid sortBy. Allowed values: {string.Join(", ", allowedSortBy)}");
                }

                facilityId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.FacilityId);
                patientId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.PatientId);
                reportId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.ReportId);
                resourceId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.ResourceId);
                var searchTerm = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.SearchTerm);

                var result = await _logQueries.SearchQueryLogSummaryAsync(
                    new SearchDataAcquisitionLogRequest
                    {
                        FacilityId = facilityId,
                        PatientId = patientId,
                        ReportTrackingId = reportId,
                        ResourceId = resourceId,
                        QueryPhase = queryParameters.QueryPhase,
                        QueryType = queryParameters.QueryType,
                        RequestStatuses = queryParameters.Statuses,
                        AcquisitionPriority = queryParameters.Priority,
                        ResourceType = queryParameters.ResourceType,
                        PageNumber = queryParameters.PageNumber,
                        PageSize = queryParameters.PageSize,
                        SortBy = queryParameters.SortBy,
                        SortOrder = queryParameters.SortOrder,
                        IncludeDeleted = queryParameters.IncludeDeleted,
                        CreatedBefore = queryParameters.CreatedBefore,
                        SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm
                    }, cancellationToken);

                return Ok(result);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.ListItems, "Search"), ex, "An ArgumentNullException occurred while attempting to search for logs with a facility id of {id}", facilityId.Sanitize());
                return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.ListItems, "Search"), ex, "An InvalidOperationException occurred while attempting to update a log with a id of {id}", facilityId.Sanitize());
                return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.ListItems, "Search"), ex, "An ArgumentException occurred while attempting to update a log with a id of {id}", facilityId.Sanitize());
                return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.ListItems, "Search"), ex, "An Exception occurred while attempting to update a log with a id of {id}", facilityId.Sanitize());
                return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        else
        {
            return BadRequest(ModelState);
        }
    }

    /// <summary>
    /// Get a data acquisition log entry.
    /// </summary>
    /// <remarks>
    /// This endpoint retrieves a list of data acquisition logs.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="id"> The ID of the log entry to retrieve.</param>
    /// <returns>A data acquisition logs entry.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionLogModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DataAcquisitionLogModel>> GetLogEntryById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        if (id == default)
        {
            return BadRequest("ID cannot be null or zero.");
        }

        try
        {
            var logEntry = await _logQueries.GetAsync(id, cancellationToken);
            if (logEntry == null)
            {
                return NotFound();
            }

            return Ok(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetLogEntryById"), ex, "An exception occurred while attempting to get log with a id of {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get note entries for a data acquisition log entry.
    /// </summary>
    [HttpGet("{id}/notes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetNotesForLog(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
            return BadRequest("ID cannot be null or zero.");

        try
        {
            var notes = await _logNotesQueries.GetByLogIdAsync(id, cancellationToken);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetNotesForLog"), ex,
                "An exception occurred while attempting to get notes for log {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get reference resources for a data acquisition log entry.
    /// </summary>
    /// <param name="id">The ID of the log entry.</param>
    /// <param name="pageNumber">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Page size. Defaults to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id}/reference-resources")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ReferenceResourcesModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReferenceResourcesForLog(
        [FromRoute] long id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
            return BadRequest("ID cannot be null or zero.");

        try
        {
            var result = await _referenceResourcesQueries.SearchAsync(
                new SearchReferenceResourcesModel
                {
                    DataAcquisitionLogId = id,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                }, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetReferenceResourcesForLog"), ex,
                "An exception occurred while attempting to get reference resources for log {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// </summary>
    /// This endpoint retrieves a list of data acquisition logs.
    /// <param name="facilityId"></param>
    /// <param name="queryParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("facility/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IPagedModel<QueryLogSummaryModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<QueryLogSummaryModel>>> GetQueryLogSummariesForFacility(
        string facilityId,
        [FromQuery] GenericLogSearchParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest($"{nameof(facilityId)} cannot be null or empty.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var summary = await _logQueries.SearchQueryLogSummaryAsync(new SearchDataAcquisitionLogRequest
            {
                FacilityId = facilityId.SanitizeAndRemove(),
                PageNumber = queryParameters.PageNumber,
                PageSize = queryParameters.PageSize,
                SortBy = queryParameters.SortBy,
                SortOrder = queryParameters.SortOrder,
                IncludeDeleted = queryParameters.IncludeDeleted
            }, cancellationToken);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetQueryLogSummariesForFacility"), ex, "An exception occurred while attempting to get query log summaries with a facility id of {id}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get a list of data acquisition logs for a patient.
    /// </summary>
    /// <remarks>
    /// This endpoint retrieves a list of data acquisition logs.
    /// </remarks>
    /// <param name="facilityId"></param>
    /// <param name="patientId"></param>
    /// <param name="queryParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("facility/{facilityId}/patient/{patientId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IPagedModel<QueryLogSummaryModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<QueryLogSummaryModel>>> GetQueryLogSummariesForFacilityAndPatient(
        string facilityId,
        string patientId,
        [FromQuery] GenericLogSearchParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest($"{nameof(facilityId)} cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            return BadRequest($"{nameof(patientId)} cannot be null or empty.");
        }

        try
        {
            var summary = await _logQueries.SearchQueryLogSummaryAsync(
                new SearchDataAcquisitionLogRequest
                {
                    FacilityId = facilityId.SanitizeAndRemove(),
                    PatientId = patientId.SanitizeAndRemove(),
                    PageNumber = queryParameters.PageNumber,
                    PageSize = queryParameters.PageSize,
                    SortBy = queryParameters.SortBy,
                    SortOrder = queryParameters.SortOrder,
                    IncludeDeleted = queryParameters.IncludeDeleted
                }, cancellationToken);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetQueryLogSummariesForFacilityAndPatient"), ex, "An exception occurred while attempting to get query log summaries with a facility id of {id}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get data acquisition log statistics for a report.
    /// </summary>
    /// <param name="reportId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("report/{reportId}/statistics")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionLogStatistics))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DataAcquisitionLogStatistics>> GetReportStatistics(
        [FromRoute] string reportId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            return BadRequest($"{nameof(reportId)} cannot be null or empty.");
        }

        //sanitize reportId
        reportId = HtmlInputSanitizer.Sanitize(reportId).SanitizeAndRemove();

        try
        {
            var statistics = await _logQueries.GetDataAcquisitionLogStatisticsByReportAsync(reportId, cancellationToken);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetReportStatistics"), ex, "An exception occurred while attempting to get report statistics with a report id of {id}", reportId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get data acquisition log status counts for a report.
    /// </summary>
    /// <param name="reportId"></param>
    /// <param name="patientId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("report/{reportId}/status-counts")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionLogStatusStatistics))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DataAcquisitionLogStatusStatistics>> GetReportStatusCounts(
        [FromRoute] string reportId,
        [FromQuery] string? patientId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            return BadRequest($"{nameof(reportId)} cannot be null or empty.");
        }

        reportId = HtmlInputSanitizer.Sanitize(reportId).SanitizeAndRemove();
        patientId = string.IsNullOrWhiteSpace(patientId)
            ? null
            : HtmlInputSanitizer.Sanitize(patientId).SanitizeAndRemove();

        try
        {
            var statistics = await _logQueries.GetDataAcquisitionLogStatusStatisticsByReportAsync(reportId, patientId, cancellationToken);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetReportStatusCounts"), ex, "An exception occurred while attempting to get report status counts with a report id of {id}", reportId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Update a data acquisition log entry.
    /// </summary>
    /// <remarks>
    /// This endpoint updates a data acquisition log entry.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="id"> The ID of the log entry to update.</param>
    /// <param name="updateModel">The model containing the updated data acquisition log entry.</param>
    /// <returns>The updated data acquisition log entry.</returns>
    /// <response code="202">Returns the updated data acquisition log entry.</response>
    /// <response code="400">If the ID is null or empty.</response>
    /// <response code="404">If the data acquisition log entry is not found.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateLogEntry(
    string id,
    UpdateDataAcquisitionLogModel updateModel,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("ID cannot be null or empty.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                updateModel.Id = long.Parse(id);
                await _logManager.UpdateAsync(updateModel, cancellationToken);
                var updated = _logQueries.GetAsync(updateModel.Id.Value, cancellationToken).Result;
                return Accepted(updated);
            }
            catch (DataAcquisitionLogNotFoundException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.UpdateItemNotFound, "UpdateLogEntry"), ex, "An DataAcquisitionLogNotFoundException occurred while attempting to update a log with a id of {id}", id.Sanitize());
                return Problem(title: "NotFound", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateLogEntry"), ex, "An ArgumentNullException occurred while attempting to update a log with a id of {id}", id.Sanitize());
                return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateLogEntry"), ex, "An InvalidOperationException occurred while attempting to update a log with a id of {id}", id.Sanitize());
                return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateLogEntry"), ex, "An ArgumentException occurred while attempting to update a log with a id of {id}", id.Sanitize());
                return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateLogEntry"), ex, "An Exception occurred while attempting to update a log with a id of {id}", id.Sanitize());
                return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        else
        {
            return BadRequest(ModelState);
        }
    }

    /// <summary>
    /// Delete a data acquisition log entry.
    /// </summary>
    /// <remarks>
    /// This endpoint deletes a data acquisition log entry by its ID.
    /// </remarks>
    /// <param name="id">The ID of the log entry to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A response indicating the result of the deletion.</returns>
    /// <response code="204">If the log entry was successfully deleted.</response>
    /// <response code="400">If the ID is null or empty.</response>
    /// <response code="404">If the log entry is not found.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteLogEntry(
        long id,
        CancellationToken cancellationToken)
    {
        if (id == default)
        {
            return BadRequest("ID cannot be null or zero.");
        }

        try
        {
            await _logManager.DeleteAsync(id, cancellationToken);

            return NoContent();
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteLogEntry"), ex, "An DataAcquisitionLogNotFoundException occurred while attempting to delete a log with a id of {id}", id);
            return Problem(title: "NotFound", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteLogEntry"), ex, "An ArgumentNullException occurred while attempting to delete a log with a id of {id}", id);
            return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteLogEntry"), ex, "An Exception occurred while attempting to delete a log with a id of {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Soft delete all data acquisition log entries for a facility.
    /// </summary>
    /// <param name="facilityId">The facility ID whose logs should be soft deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of records soft deleted.</returns>
    /// <response code="200">Returns the count of soft deleted records.</response>
    /// <response code="400">If the facility ID is null or empty.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpDelete("facility/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SoftDeleteByFacility(
        string facilityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("facilityId cannot be null or empty.");
        }

        try
        {
            await _logManager.SoftDeleteByFacilityAsync(facilityId.SanitizeAndRemove(), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "SoftDeleteByFacility"), ex, "An exception occurred while attempting to soft delete logs for facility {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Soft delete all data acquisition log entries for a report tracking ID.
    /// </summary>
    /// <param name="reportTrackingId">The report tracking ID whose logs should be soft deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Logs were successfully soft deleted.</response>
    /// <response code="400">If the report tracking ID is null or empty.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpDelete("report/{reportTrackingId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SoftDeleteByReportTrackingId(
        string reportTrackingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportTrackingId))
        {
            return BadRequest("reportTrackingId cannot be null or empty.");
        }

        try
        {
            await _logManager.SoftDeleteByReportTrackingIdAsync(reportTrackingId.SanitizeAndRemove(), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "SoftDeleteByReportTrackingId"), ex, "An exception occurred while attempting to soft delete logs for report tracking ID {reportTrackingId}", reportTrackingId.Sanitize());
            return Problem(title: "Internal Server Error", statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Restore soft deleted data acquisition log entries for a report tracking ID.
    /// </summary>
    /// <param name="reportTrackingId">The report tracking ID whose logs should be restored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Logs were successfully restored.</response>
    /// <response code="400">If the report tracking ID is null or empty.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpPatch("report/{reportTrackingId}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RestoreByReportTrackingId(
        string reportTrackingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportTrackingId))
        {
            return BadRequest("reportTrackingId cannot be null or empty.");
        }

        try
        {
            await _logManager.RestoreByReportTrackingIdAsync(reportTrackingId.SanitizeAndRemove(), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "RestoreByReportTrackingId"), ex, "An exception occurred while attempting to restore logs for report tracking ID {reportTrackingId}", reportTrackingId.Sanitize());
            return Problem(title: "Internal Server Error", statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Restore soft deleted data acquisition log entries for a facility.
    /// </summary>
    /// <param name="facilityId">The facility ID whose logs should be restored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of records restored.</returns>
    /// <response code="200">Returns the count of restored records.</response>
    /// <response code="400">If the facility ID is null or empty.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpPatch("facility/{facilityId}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RestoreByFacility(
        string facilityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("facilityId cannot be null or empty.");
        }

        try
        {
            var count = await _logManager.RestoreByFacilityAsync(facilityId.SanitizeAndRemove(), cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "RestoreByFacility"), ex, "An exception occurred while attempting to restore logs for facility {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Process a data acquisition log entry.
    /// </summary>
    /// <returns>
    /// A response indicating the result of the processing.
    /// </returns>
    [HttpPost("{id}/process")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Process(long id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return BadRequest("ID cannot be null or zero.");
        }

        try
        {
            await _logService.StartRetrievalProcess(id, cancellationToken);

            return Accepted();
        }
        catch (DataAcquisitionLogNotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Process"), ex, "An DataAcquisitionLogNotFoundException occurred while attempting to process a log with a id of {id}", id);
            return Problem(title: "NotFound", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Process"), ex, "An ArgumentNullException occurred while attempting to process a log with a id of {id}", id);
            return Problem(title: "BadRequest", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Process"), ex, "An Exception occurred while attempting to process a log with a id of {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Process multiple data acquisition log entries.
    /// </summary>
    /// <returns>
    /// A response indicating the result of the processing.
    /// </returns>
    [HttpPost("process-bulk")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessBulk([FromBody] List<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            return BadRequest("IDs cannot be null or empty.");
        }

        try
        {
            await _logService.StartRetrievalProcessBulk(ids, cancellationToken);

            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "ProcessBulk"), ex, "An Exception occurred while attempting to process logs in bulk.");
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Cancel multiple data acquisition log entries.
    /// </summary>
    /// <param name="ids">The IDs of the logs to cancel.</param>
    /// <param name="minAgeHours">Minimum age in hours a log must have to be eligible for cancellation. Defaults to 24.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// A response indicating the result of the cancellation.
    /// </returns>
    [ValidateAntiForgeryOrBearerToken]
    [HttpPost("cancel-bulk")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelBulk([FromBody] List<long> ids, [FromQuery] int minAgeHours = DefaultCancelMinAgeHours, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            return BadRequest("IDs cannot be null or empty.");
        }

        if (minAgeHours < 0) 
        { 
            return BadRequest("minAgeHours must be zero or greater.");
        }
        try
        {
            var cancelledCount = await _logManager.CancelBulkAsync(ids, minAgeHours, cancellationToken);
            return Accepted(new { requested = ids.Count, cancelled = cancelledCount, ineligible = ids.Count - cancelledCount });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "CancelBulk"), ex, "An Exception occurred while attempting to cancel logs in bulk.");
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Process data acquisition log entries based on search criteria.
    /// </summary>
    /// <returns>
    /// A response indicating the result of the processing.
    /// </returns>
    [HttpPost("process-by-filter")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessByFilter([FromBody] LogSearchParameters queryParameters, CancellationToken cancellationToken = default)
    {
        if (queryParameters == null)
        {
            return BadRequest("Query parameters are required.");
        }

        if (string.IsNullOrWhiteSpace(queryParameters.FacilityId) &&
            string.IsNullOrWhiteSpace(queryParameters.PatientId) &&
            string.IsNullOrWhiteSpace(queryParameters.ReportId) &&
            string.IsNullOrWhiteSpace(queryParameters.ResourceId) &&
            !queryParameters.QueryPhase.HasValue &&
            !queryParameters.QueryType.HasValue &&
            (queryParameters.Statuses == null || !queryParameters.Statuses.Any()) &&
            !queryParameters.Priority.HasValue &&
            string.IsNullOrWhiteSpace(queryParameters.ResourceType))
        {
            return BadRequest("At least one filter criteria must be provided.");
        }

        try
        {
            var facilityId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.FacilityId);
            var patientId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.PatientId);
            var reportId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.ReportId);
            var resourceId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.ResourceId);

            var result = await _logQueries.SearchAsync(
                new SearchDataAcquisitionLogRequest
                {
                    FacilityId = facilityId,
                    PatientId = patientId,
                    ReportTrackingId = reportId,
                    ResourceId = resourceId,
                    QueryPhase = queryParameters.QueryPhase,
                    QueryType = queryParameters.QueryType,
                    RequestStatuses = queryParameters.Statuses,
                    AcquisitionPriority = queryParameters.Priority,
                    ResourceType = queryParameters.ResourceType,
                    IncludeDeleted = queryParameters.IncludeDeleted,
                    CreatedBefore = queryParameters.CreatedBefore,
                    PageNumber = 1,
                    PageSize = int.MaxValue // Get all matching IDs
                }, cancellationToken);

            if (result.Records != null && result.Records.Any())
            {
                var ids = result.Records.Select(r => r.Id).ToList();
                await _logService.StartRetrievalProcessBulk(ids, cancellationToken);
            }

            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "ProcessByFilter"), ex, "An Exception occurred while attempting to process logs by filter.");
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Cancel data acquisition log entries based on search criteria.
    /// </summary>
    /// <returns>
    /// A response indicating the result of the cancellation.
    /// </returns>
    [ValidateAntiForgeryOrBearerToken]
    [HttpPost("cancel-by-filter")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelByFilter([FromBody] LogSearchParameters queryParameters, [FromQuery] int minAgeHours = DefaultCancelMinAgeHours, CancellationToken cancellationToken = default)
    {
        if (queryParameters == null)
        {
            return BadRequest("Query parameters are required.");
        }

        if (minAgeHours < 0)
        {
            return BadRequest("minAgeHours must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(queryParameters.FacilityId) &&
            string.IsNullOrWhiteSpace(queryParameters.PatientId) &&
            string.IsNullOrWhiteSpace(queryParameters.ReportId) &&
            string.IsNullOrWhiteSpace(queryParameters.ResourceId) &&
            !queryParameters.QueryPhase.HasValue &&
            !queryParameters.QueryType.HasValue &&
            (queryParameters.Statuses == null || !queryParameters.Statuses.Any()) &&
            !queryParameters.Priority.HasValue &&
            string.IsNullOrWhiteSpace(queryParameters.ResourceType))
        {
            return BadRequest("At least one filter criteria must be provided.");
        }

        try
        {
            var facilityId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.FacilityId);
            var patientId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.PatientId);
            var reportId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.ReportId);
            var resourceId = HtmlInputSanitizer.SanitizeAndRemove(queryParameters.ResourceId);

            var filter = new SearchDataAcquisitionLogRequest
            {
                FacilityId = facilityId,
                PatientId = patientId,
                ReportTrackingId = reportId,
                ResourceId = resourceId,
                QueryPhase = queryParameters.QueryPhase,
                QueryType = queryParameters.QueryType,
                RequestStatuses = queryParameters.Statuses,
                AcquisitionPriority = queryParameters.Priority,
                ResourceType = queryParameters.ResourceType,
                IncludeDeleted = queryParameters.IncludeDeleted,
                CreatedBefore = queryParameters.CreatedBefore
            };

            var (requested, cancelled) = await _logManager.CancelByFilterAsync(filter, minAgeHours, cancellationToken);

            return Accepted(new { requested, cancelled, ineligible = requested - cancelled });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "CancelByFilter"), ex, "An Exception occurred while attempting to cancel logs by filter.");
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Returns a lightweight summary of data acquisition activity for a report.
    /// Totals, status counts and resource-type counts computed via DB aggregates.
    /// Intended for dashboard polling and run monitoring.
    /// </summary>
    [HttpGet("report/{reportId}/summary")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionReportSummaryApiModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DataAcquisitionReportSummaryApiModel>> GetReportSummary(
        [FromRoute] string reportId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            return BadRequest($"{nameof(reportId)} cannot be null or empty.");

        reportId = HtmlInputSanitizer.Sanitize(reportId).SanitizeAndRemove();

        try
        {
            var summary = await _logQueries.GetDataAcquisitionLogStatisticsByReportAsync(reportId, cancellationToken);

            return Ok(new DataAcquisitionReportSummaryApiModel
            {
                ReportId = reportId,
                TotalLogs = summary.TotalLogs,
                TotalPatients = summary.TotalPatients,
                TotalCompletedPatients = summary.TotalCompletedPatients,
                TotalResourcesAcquired = summary.TotalResourcesAcquired,
                TotalRetryAttempts = summary.TotalRetryAttempts,
                TotalCompletionTimeMs = summary.TotalCompletionTimeMilliseconds,
                StatusCounts = summary.RequestStatusCounts.Select(s => new DataAcquisitionReportSummaryApiModel.StatusCountEntry
                {
                    Status = s.Key.ToString(),
                    Count = s.Value
                }).ToList(),
                ResourceTypeCounts = summary.ResourceTypeCounts.Select(r => new DataAcquisitionReportSummaryApiModel.ResourceTypeCountEntry
                {
                    ResourceType = r.Key,
                    Count = r.Value
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetReportSummary"), ex,
                "An exception occurred while attempting to get report summary for reportId {reportId}", reportId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get all ResourceAcquiredIds from completed logs for a facility/report pair.
    /// Intended for automation reconciliation.
    /// </summary>
    [HttpGet("report/{reportId}/acquired-resource-ids")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> GetAcquiredResourceIdsForReport(
        [FromRoute] string reportId,
        [FromQuery] string facilityId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            return BadRequest("reportId cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId cannot be null or empty.");

        try
        {
            // Use the statistics query which already extracts resource IDs
            // from the ResourceIds junction table for completed logs.
            var statistics = await _logQueries.GetDataAcquisitionLogStatisticsByReportAsync(
                reportId.SanitizeAndRemove(), cancellationToken);

            // The statistics method aggregates resource type counts from completed logs' ResourceIds.
            // We need the raw IDs, so query via SearchQueryLogSummary with full model to get them.
            var pageNumber = 1;
            const int pageSize = 500;
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var page = await _logQueries.SearchAsync(new SearchDataAcquisitionLogRequest
                {
                    FacilityId = facilityId.SanitizeAndRemove(),
                    ReportTrackingId = reportId.SanitizeAndRemove(),
                    RequestStatuses = [RequestStatus.Completed],
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SortBy = "Id",
                    SortOrder = LantanaGroup.Link.Shared.Application.Enums.SortOrder.Ascending
                }, cancellationToken);

                if (page?.Records == null || page.Records.Count == 0)
                    break;

                foreach (var log in page.Records)
                {
                    // For each completed log, fetch full detail to get resource IDs
                    var full = await _logQueries.GetAsync(log.Id, cancellationToken);
                    if (full?.ResourceAcquiredIds == null) continue;

                    foreach (var id in full.ResourceAcquiredIds.Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains('/')))
                        results.Add(id);
                }

                if (page.Records.Count < pageSize)
                    break;

                pageNumber++;
            }

            return Ok(results.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetAcquiredResourceIdsForReport"), ex,
                "An exception occurred while attempting to get acquired resource ids for report {reportId}", reportId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
