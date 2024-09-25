using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[Route("api/data/{facilityId}/[controller]")]
public class FhirQueriesController : ControllerBase
{
    private readonly ILogger<FhirQueriesController> _logger;
    private IFhirQueryManager _fhirQueryManager;

    public FhirQueriesController(ILogger<FhirQueriesController> logger, IFhirQueryManager fhirQueryManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
    }

    /// <summary>
    /// Get query results for a correlation Id
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="correlationId"></param>
    /// <param name="patientId"></param>
    /// <param name="resourceType"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="queryType"></param>
    /// <param name="successOnly"></param>
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
        [FromRoute] string facilityId, [FromQuery] string correlationId, [FromQuery] string patientId, [FromQuery] string resourceType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId)) 
        {
            return BadRequest("facility Id is empty");
        }

        try
        {
            var results =
                await _fhirQueryManager.GetFhirQueriesAsync(facilityId, correlationId, patientId, resourceType, cancellationToken);

            if (results.Queries.Count == 0)
            {
                return NotFound();
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            var sanitizedCorrelationId = correlationId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "").Trim();
            _logger.LogError(new EventId(DataAcquisitionConstants.LoggingIds.GetItem, "Get FHIR Query Results"), ex, "An exception occurred while attempting to retrieve FHIR Query Results for correlationId {correlationId}", sanitizedCorrelationId);
            throw;
        }
    }
}
