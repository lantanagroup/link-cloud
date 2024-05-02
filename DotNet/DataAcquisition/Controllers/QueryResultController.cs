using LantanaGroup.Link.DataAcquisition.Application.Commands.QueryResult;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[ApiController]
[Route("api/{facilityId}/[controller]")]
public class QueryResultController : ControllerBase
{
    private readonly ILogger<QueryResultController> _logger;
    private readonly IMediator _mediator;

    public QueryResultController(ILogger<QueryResultController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Get query results for a patient
    /// </summary>
    /// <param name="correlationId"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="queryType"></param>
    /// <param name="SuccessOnly"></param>
    /// <returns></returns>
    [HttpGet("{correlationId}")]
    public async Task<ActionResult<QueryResultsModel>> GetPatientQueryResults(CancellationToken cancellationToken, [FromRoute]string correlationId, string? queryType, bool SuccessOnly = true)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) 
        {
            return BadRequest("Correlation Id is empty");
        }

        try
        {
            return Ok(await _mediator.Send(new GetPatientQueryResultsQuery { CorrelationId = correlationId, QueryType = queryType, SuccessOnly = SuccessOnly }, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patient query results");
            return StatusCode(500, "Error getting patient query results");
        }
    }
}
