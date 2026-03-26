using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
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
    /// GET: api/patient-encounters/current?facilityId={facilityId}&correlationId={correlationId}&sortBy={sortBy}&sortOrder={sortOrder}&pageSize={pageSize}&pageNumber={pageNumber}
    /// </remarks>
    /// <param name="facilityId">The unique identifier for the facility. (Required)</param>
    /// <param name="correlationId">Optional correlation ID to filter patient encounters.</param>
    /// <param name="sortBy">Optional field to sort by (e.g., "CorrelationId", "AdmitDate").</param>
    /// <param name="sortOrder">Optional sort order (Ascending or Descending).</param>
    /// <param name="pageSize">Number of records per page (default: 10).</param>
    /// <param name="pageNumber">Page number to retrieve (default: 1).</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A paged list of current patient encounter models for the specified facility.</returns>
    [HttpGet("current")]
    public async Task<ActionResult<PagedConfigModel<PatientEncounterModel>>> GetCurrentPatientEncounters(
        string facilityId,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        facilityId = HtmlInputSanitizer.SanitizeAndRemove(facilityId ?? string.Empty);
        correlationId = string.IsNullOrEmpty(correlationId) ? null : HtmlInputSanitizer.SanitizeAndRemove(correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");

        try
        {
            var pagedEncounters = await _patientEncounterQueries.GetPagedCurrentPatientEncounters(
                facilityId,
                correlationId,
                sortBy,
                sortOrder,
                pageSize,
                pageNumber,
                cancellationToken
            );

            return Ok(pagedEncounters);
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
    /// GET: api/patient-encounters/historical?facilityId={facilityId}&correlationId={correlationId}&dateThreshold={dateThreshold}&sortBy={sortBy}&sortOrder={sortOrder}&pageSize={pageSize}&pageNumber={pageNumber}
    /// </remarks>
    /// <param name="facilityId">The unique identifier for the facility. (Required)</param>
    /// <param name="correlationId">Optional correlation ID to filter patient encounters.</param>
    /// <param name="dateThreshold">The date as of which to generate the historical view. (Required)</param>
    /// <param name="sortBy">Optional field to sort by (e.g., "CorrelationId", "AdmitDate").</param>
    /// <param name="sortOrder">Optional sort order (Ascending or Descending).</param>
    /// <param name="pageSize">Number of records per page (default: 10).</param>
    /// <param name="pageNumber">Page number to retrieve (default: 1).</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A paged list of patient encounter models as of the specified date for the facility.</returns>
    [HttpGet("historical")]
    public async Task<ActionResult<PagedConfigModel<PatientEncounterModel>>> GetHistoricalMaterializedView(
        string facilityId,
        string? correlationId = null,
        DateTime? dateThreshold = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        facilityId = HtmlInputSanitizer.SanitizeAndRemove(facilityId ?? string.Empty);
        correlationId = string.IsNullOrEmpty(correlationId) ? null : HtmlInputSanitizer.SanitizeAndRemove(correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");
        if (!dateThreshold.HasValue)
            return BadRequest("dateThreshold is required.");

        try
        {
            var pagedHistoricalView = await _patientEncounterQueries.GetPagedViewAsOf(
                facilityId,
                dateThreshold.Value,
                correlationId,
                sortBy,
                sortOrder,
                pageSize,
                pageNumber,
                cancellationToken
            );

            return Ok(pagedHistoricalView);
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
        string facilityId,
        string? correlationId = default,
        CancellationToken cancellationToken = default)
    {
        facilityId = HtmlInputSanitizer.SanitizeAndRemove(facilityId ?? string.Empty);
        correlationId = string.IsNullOrEmpty(correlationId) ? null : HtmlInputSanitizer.SanitizeAndRemove(correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");

        try
        {
            await _patientEncounterQueries.RebuildPatientEncounterTable(facilityId, correlationId,cancellationToken: cancellationToken);
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