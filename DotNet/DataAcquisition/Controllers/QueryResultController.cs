using DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[Route("api/data-acquisition/{facilityId}/[controller]")]
public class FhirQueriesController : ControllerBase
{
    private readonly ILogger<FhirQueriesController> _logger;
    private IFhirQueryManager _fhirQueryManager;
    private IFhirQueryQueries _fhirQueryQueries;

    public FhirQueriesController(ILogger<FhirQueriesController> logger, IFhirQueryManager fhirQueryManager, IFhirQueryQueries fhirQueryQueries)
    {
        _logger = logger;
        _fhirQueryManager = fhirQueryManager;
        _fhirQueryQueries = fhirQueryQueries;
    }

    /// <summary>
    /// Get query results for a correlation Id
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="correlationId"></param>
    /// <param name="patientId"></param>
    /// <param name="resourceType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// Success: 200
    /// Bad Request: 400
    /// Not Found: 404
    /// Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(QueryResultsModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet]
    public async Task<ActionResult<QueryResultsModel>> GetQueries(
        [FromRoute] string facilityId, [FromQuery] string? correlationId, [FromQuery] string? patientId, [FromQuery] string? resourceType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("facility Id is empty");
        }

        try
        {
            var results = await _fhirQueryQueries.GetFhirQueriesAsync(facilityId, correlationId, patientId, resourceType, cancellationToken);

            // Queries is unset when the read produced no model. That is not-found, same as an empty list.
            if (results?.Queries is not { Count: > 0 })
            {
                return NotFound();
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            // correlationId is optional. Calling Replace on a null value threw NullReferenceException
            // and hid the exception from the read.
            var sanitizedCorrelationId = HtmlInputSanitizer.SanitizeAndRemove(correlationId ?? string.Empty);
            _logger.LogError(new EventId(DataAcquisitionConstants.LoggingIds.GetItem, "Get FHIR Query Results"), ex, "An exception occurred while attempting to retrieve FHIR Query Results for correlationId {correlationId}", sanitizedCorrelationId);
            throw;
        }
    }
}
