using LantanaGroup.Link.Census.Application.Models.Api;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Census.Controllers;

[Route("api/census/patient-encounters/")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class PatientEncountersController : Controller
{
    private readonly ILogger<PatientEncountersController> _logger;
    private readonly IPatientEncounterManager _patientEncounterManager;
    private readonly IPatientEncounterQueries _patientEncounterQueries;

    public PatientEncountersController(
        ILogger<PatientEncountersController> logger,
        IPatientEncounterManager patientEncounterManager,
        IPatientEncounterQueries patientEncounterQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientEncounterManager = patientEncounterManager ?? throw new ArgumentNullException(nameof(patientEncounterManager));
        _patientEncounterQueries = patientEncounterQueries ?? throw new ArgumentNullException(nameof(patientEncounterQueries));
    }

    /// <summary>
    /// Returns the current materialized view state for patient(s) events for a given facility.
    /// </summary>
    /// <remarks>
    /// GET: api/patient-encounters/current?facilityId={facilityId}&correlationId={correlationId}
    /// </remarks>
    /// <param name="facilityId">The unique identifier for the facility. (Required)</param>
    /// <param name="correlationId">Optional correlation ID to filter patient encounters.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A list of current patient event models for the specified facility.</returns>
    [HttpGet("current")]
    public async Task<ActionResult<IEnumerable<PatientEventModel>>> GetCurrentPatientEncounters(
        [FromQuery] string facilityId,
        [FromQuery] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        facilityId = HtmlInputSanitizer.SanitizeAndRemove(facilityId ?? string.Empty);
        correlationId = string.IsNullOrEmpty(correlationId) ? null : HtmlInputSanitizer.SanitizeAndRemove(correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");

        try
        {
            var patientEncounters = await _patientEncounterManager.GetPatientEncounterModels(
                facilityId,
                correlationId,
                cancellationToken
            );

            return Ok(patientEncounters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patient encounters for facility {FacilityId}", facilityId?.Replace("\r", "").Replace("\n", ""));
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Returns an ad hoc generated materialized view state for patient(s) events for a given facility as of a specific date.
    /// </summary>
    /// <remarks>
    /// GET: api/patient-encounters/historical?facilityId={facilityId}&correlationId={correlationId}&dateThreshold={dateThreshold}
    /// </remarks>
    /// <param name="facilityId">The unique identifier for the facility. (Required)</param>
    /// <param name="correlationId">Optional correlation ID to filter patient encounters.</param>
    /// <param name="dateThreshold">The date as of which to generate the historical view. (Required)</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A list of patient encounter models as of the specified date for the facility.</returns>
    [HttpGet("historical")]
    public async Task<ActionResult<IEnumerable<PatientEncounterModel>>> GetHistoricalMaterializedView(
        [FromQuery] string facilityId,
        [FromQuery] string? correlationId = null,
        [FromQuery] DateTime? dateThreshold = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");
        if (!dateThreshold.HasValue)
            return BadRequest("dateThreshold is required.");

        try
        {
            var historicalView = await _patientEncounterQueries.GetViewAsOf(
                facilityId,
                dateThreshold.Value,
                correlationId,
                cancellationToken
            );
            if (historicalView is null || !historicalView.Any())
            {
                return NotFound($"No historical materialized view found for facility {facilityId} as of {dateThreshold.Value}.");
            }
            return Ok(historicalView);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving historical materialized view for facility {FacilityId}", facilityId?.Replace("\r", "").Replace("\n", ""));
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Deletes and rebuilds the materialized view records for a given facility.
    /// </summary>
    /// <remarks>
    /// POST: api/patient-encounters/rebuild?facilityId={facilityId}&correlationId={correlationId}
    /// </remarks>
    /// <param name="facilityId">The unique identifier for the facility. (Required)</param>
    /// <param name="correlationId">Optional correlation ID to filter which materialized view to rebuild.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>Accepted if the rebuild is successful; error details otherwise.</returns>
    [HttpPost("rebuild")]
    public async Task<IActionResult> RebuildMaterializedView(
        [FromQuery] string facilityId,
        [FromQuery] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");

        try
        {
            await _patientEncounterQueries.RebuildPatientEncounterTable(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding materialized view for facility {FacilityId}", facilityId?.Replace("\r", "").Replace("\n", ""));
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        return Accepted();
    }
}
