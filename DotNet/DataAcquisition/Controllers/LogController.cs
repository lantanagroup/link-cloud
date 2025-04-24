using DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/acquisition-logs")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class LogController : Controller
{
    private readonly ILogger<LogController> _logger;
    private readonly IDataAcquisitionLogManager _logManager;

    public LogController(ILogger<LogController> logger, IDataAcquisitionLogManager logManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
    }

    /// <summary>
    /// Get a list of data acquisition logs.
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
    public async Task<ActionResult<DataAcquisitionLog>> GetLogEntryById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var logEntry = await _logManager.GetAsync(id, cancellationToken);
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
}
