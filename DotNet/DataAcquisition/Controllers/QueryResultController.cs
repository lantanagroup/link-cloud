using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[Route("api/data/{facilityId}/[controller]")]
public class QueryResultController : ControllerBase
{
    private readonly ILogger<QueryResultController> _logger;
    private IQueriedFhirResourceManager _queriedFhirResourceManager;

    public QueryResultController(ILogger<QueryResultController> logger, IQueriedFhirResourceManager queriedFhirResourceManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queriedFhirResourceManager = queriedFhirResourceManager ?? throw new ArgumentNullException(nameof(queriedFhirResourceManager));
    }

    /// <summary>
    /// Get query results for a correlation Id
    /// </summary>
    /// <param name="correlationId"></param>
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
    [HttpGet("{correlationId}")]
    public async Task<ActionResult<QueryResultsModel>> GetQueryResults(CancellationToken cancellationToken, [FromRoute]string correlationId, string queryType = "", bool successOnly = true)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) 
        {
            return BadRequest("Correlation Id is empty");
        }

        try
        {
            var results =
                await _queriedFhirResourceManager.GetQueryResultsAsync(queryType, correlationId, successOnly,
                    cancellationToken);

            if (results.QueryResults.Count == 0)
            {
                return NotFound();
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(DataAcquisitionConstants.LoggingIds.GetItem, "Get Query Results"), ex, "An exception occurred while attempting to retrieve Query Results for correlationId {correlationId}", correlationId);
            throw;
        }
    }
}
