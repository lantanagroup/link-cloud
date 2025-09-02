using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LantanaGroup.Link.Shared.Application.Enums;
using System.Net;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/acquisition-logs")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class LogController : Controller
{
    private readonly ILogger<LogController> _logger;
    private readonly IDataAcquisitionLogService _logService;
    private readonly IDataAcquisitionLogManager _logManager;

    public LogController(ILogger<LogController> logger, IDataAcquisitionLogService logService, IDataAcquisitionLogManager logManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
    }

    /// <summary>
    /// Get a list of data acquisition logs.
    /// </summary>
    /// <remarks>
    /// This endpoint retrieves a list of data acquisition logs.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="facilityId">The ID of the facility.</param>
    /// <param name="patientId">The ID of the patient.</param>
    /// <param name="reportId"></param>
    /// <param name="resourceId"></param>
    /// <param name="queryPhase">The phase of the query.</param>
    /// <param name="queryType"></param>
    /// <param name="status">The status of the request.</param>
    /// <param name="priority">The priority of the acquisition.</param>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="sortBy">The field to sort by.</param>
    /// <param name="sortOrder">The order to sort by (ascending or descending).</param>
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
        [FromQuery] string? facilityId,
        [FromQuery] string? patientId,
        [FromQuery] string? reportId,
        [FromQuery] string? resourceId,
        [FromQuery] QueryPhase? queryPhase,
        [FromQuery] FhirQueryType? queryType,
        [FromQuery] RequestStatus? status,
        [FromQuery] AcquisitionPriority? priority,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "ExecutionDate",
        [FromQuery] SortOrder sortOrder = SortOrder.Descending,
        CancellationToken cancellationToken = default
        ) 
    {
        if(pageNumber < 1)
        {
            return BadRequest("Page number must be greater than or equal to 1.");
        }
        
        if(pageSize < 1)
        {
            return BadRequest("Page size must be greater than or equal to 1.");
        }
        
        try
        {
            var result = await _logManager.SearchAsync( 
                new SearchDataAcquisitionLogRequest 
                {
                    FacilityId = facilityId,
                    PatientId = patientId,
                    ReportId = reportId,
                    ResourceId = resourceId,
                    QueryPhase = queryPhase,
                    QueryType = queryType,
                    RequestStatus = status,
                    AcquisitionPriority = priority,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SortBy = sortBy,
                    SortOrder = sortOrder
                }, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionLog))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DataAcquisitionLogModel>> GetLogEntryById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("ID cannot be null or empty.");
        }

        try
        {
            var logEntry = await _logManager.GetModelAsync(id, cancellationToken);
            if (logEntry == null)
            {
                return NotFound();
            }

            return Ok(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get a list of data acquisition logs for a facility.
    /// </summary>
    /// <remarks>
    /// This endpoint retrieves a list of data acquisition logs.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="id"> The ID of the log entry to retrieve.</param>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="sortBy">The field to sort by.</param>
    /// <param name="sortOrder">The order to sort by (ascending or descending).</param>
    /// <returns>A list of data acquisition logs.</returns>
    [HttpGet("facility/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionLog))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<QueryLogSummaryModel>>> GetQueryLogSummariesForFacility(
        [FromRoute] string facilityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "ExecutionDate",
        [FromQuery] SortOrder sortOrder = SortOrder.Descending,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest($"{nameof(facilityId)} cannot be null or empty.");
        }

        try
        {
            var summary = await _logManager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, cancellationToken);
            if (summary == null)
            {
                return NotFound();
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
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
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="sortBy">The field to sort by.</param>
    /// <param name="sortOrder">The order to sort by (ascending or descending).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of data acquisition logs.</returns>
    [HttpGet("facility/{facilityId}/patient/{patientId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DataAcquisitionLog))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<QueryLogSummaryModel>>> GetQueryLogSummariesForFacilityAndPatient(
        [FromRoute] string facilityId,
        [FromRoute] string patientId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "ExecutionDate",
        [FromQuery] SortOrder sortOrder = SortOrder.Descending,
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
            var summary = await _logManager.SearchAsync(
                new SearchDataAcquisitionLogRequest 
                {
                    FacilityId = facilityId,
                    PatientId = patientId,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SortBy = sortBy,
                    SortOrder = sortOrder
                }, cancellationToken);
            if (summary == null)
            {
                return NotFound();
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
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
        reportId = HtmlInputSanitizer.Sanitize(reportId);

        try
        {
            var statistics = await _logManager.GetStatisticsByReportAsync(reportId, cancellationToken);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
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
    [FromRoute] string id,
    [FromBody] UpdateDataAcquisitionLogModel updateModel,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("ID cannot be null or empty.");
        }

        try
        {
            var updatedLog = await _logManager.UpdateAsync(updateModel, cancellationToken);

            return Accepted(updatedLog);
        }
        catch (DataAcquisitionLogNotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return NotFound(ex.Message);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
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
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("ID cannot be null or empty.");
        }

        try
        {
            await _logManager.DeleteAsync(id, cancellationToken);

            return NoContent();
        }
        catch (DataAcquisitionLogNotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return NotFound(ex.Message);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
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
    public async Task<IActionResult> Process(string id, CancellationToken cancellationToken = default) 
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("ID cannot be null or empty.");
        }

        try
        {
            await _logService.StartRetrievalProcess(id, cancellationToken);

            return Accepted();
        }
        catch (DataAcquisitionLogNotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return NotFound(ex.Message);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
