using DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LantanaGroup.Link.Shared.Application.Enums;
using System.Net;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/acquisition-logs")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class LogController : Controller
{
    private readonly ILogger<LogController> _logger;
    private readonly IDataAcquisitionLogService _logService;

    public LogController(ILogger<LogController> logger, IDataAcquisitionLogService logService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
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
        try
        {
            var logEntry = await _logService.GetLogEntryById(id, cancellationToken);
            if (logEntry == null)
            {
                return NotFound();
            }

            return Ok(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
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
        try
        {
            var summary = await _logService.GetQueryLogSummariesForFacility(facilityId, page, pageSize, sortBy, sortOrder, cancellationToken);
            if (summary == null)
            {
                return NotFound();
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
    }
}
