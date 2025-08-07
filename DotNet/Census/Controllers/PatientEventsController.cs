using LantanaGroup.Link.Census.Application.Models.Api;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Census.Controllers;

[Route("api/patient-events/")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class PatientEventsController : Controller
{
    private readonly ILogger<PatientEventsController> _logger;
    private readonly IPatientEventManager _patientEventManager;
    private readonly IPatientEventQueries _patientEventQueries;

    public PatientEventsController(
        ILogger<PatientEventsController> logger,
        IPatientEventManager patientEventManager,
        IPatientEventQueries patientEventQueries
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientEventManager = patientEventManager ?? throw new ArgumentNullException(nameof(patientEventManager));
        _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(patientEventQueries));
    }

    /// <summary>
    /// Returns all events stored in the dbo.PatientEvents data store for the given facility.
    /// </summary>
    /// <remarks>
    /// GET: api/patient-events?facilityId={facilityId}&correlationId={correlationId}&startDate={startDate}&endDate={endDate}
    /// </remarks>
    /// <param name="facilityId">The unique identifier for the facility. (Required)</param>
    /// <param name="correlationId">Optional correlation ID to filter events.</param>
    /// <param name="startDate">Optional start date to filter events.</param>
    /// <param name="endDate">Optional end date to filter events.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A list of patient events for the specified facility.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PatientEventModel>>> GetPatientEvents(
        [FromQuery] string facilityId,
        [FromQuery] string? correlationId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");

        try
        {
            var events = await _patientEventQueries.GetPatientEvents(
                facilityId,
                correlationId,
                startDate,
                endDate,
                cancellationToken
            );

            if (events is null || !events.Any())
            {
                return NotFound($"No patient events found for facility {facilityId}.");
            }

            return Ok(events.Select(PatientEventModel.FromDomain));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patient events for facility {FacilityId}", facilityId);
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Soft deletes a patient event and rebuilds the materialized view for the related correlation id.
    /// </summary>
    /// <remarks>
    /// DELETE: api/patient-events/{id}
    /// </remarks>
    /// <param name="id">The unique identifier of the patient event to delete. (Required)</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>Accepted if deletion is successful; error details otherwise.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatientEvent(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("Patient event ID is required.");
        }

        try
        {
            await _patientEventManager.DeletePatientEventById(id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting patient event with ID {Id}", id);
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        return Accepted();
    }

    /// <summary>
    /// Soft deletes the patient event store for the given correlation id and removes the corresponding materialized view.
    /// </summary>
    /// <remarks>
    /// DELETE: api/patient-events/visit/{correlationId}
    /// </remarks>
    /// <param name="correlationId">The correlation ID for the visit to delete. (Required)</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>Accepted if deletion is successful; error details otherwise.</returns>
    [HttpDelete("visit/{correlationId}")]
    public async Task<IActionResult> DeletePatientEventsByCorrelation(string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return BadRequest("Correlation ID is required.");
        }
        
        try
        {
            await _patientEventQueries.DeletePatientEventByCorrelationId(correlationId, cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting patient events for correlation ID {CorrelationId}", correlationId);
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        return Accepted();
    }
}
