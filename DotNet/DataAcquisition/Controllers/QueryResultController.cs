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
    /// <param name="facilityId"></param>
    /// <param name="patientId"></param>
    /// <param name="correlationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("patientId")]
    public async Task<ActionResult<QueryResultsModel>> GetPatientQueryResults(string facilityId, string patientId, [FromQuery] string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _mediator.Send(new GetPatientQueryResultsQuery { FacilityId = facilityId, PatientId = patientId, CorrelationId = correlationId }, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patient query results");
            return StatusCode(500, "Error getting patient query results");
        }
    }
}
