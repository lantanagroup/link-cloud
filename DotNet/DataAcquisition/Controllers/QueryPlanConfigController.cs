using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using static LantanaGroup.Link.DataAcquisition.Application.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/{facilityId}")]
public class QueryPlanConfigController : Controller
{
    private readonly ILogger<QueryPlanConfigController> _logger;
    private readonly IMediator _mediator;

    public QueryPlanConfigController(ILogger<QueryPlanConfigController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Gets a QueryPlanConfig record for a given facilityId, queryPlanType, and systemPlans.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryPlanType"></param>
    /// <param name="systemPlans"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{queryPlanType}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IQueryPlan>> GetQueryPlan(
        string facilityId, 
        QueryPlanType queryPlanType,
        bool systemPlans,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("facilityId is required.");
        }

        try
        {
            var result = await _mediator.Send(new GetQueryPlanQuery
            {
                FacilityId = facilityId
            });

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating query plan for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
                "",
                facilityId,
                AuditEventType.Query,
                null);
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetQueryPlan"), ex, "An exception occurred while attempting to retrieve a query place with a facility id of {id}", facilityId);
            throw;
        }
    }

    /// <summary>
    /// Creates a QueryPlanConfig for a facility
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryPlanType"></param>
    /// <param name="queryPlan"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Facility Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost("{queryPlanType}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQueryPlan(
        string facilityId, 
        QueryPlanType queryPlanType, 
        [FromBody] IQueryPlan queryPlan, 
        CancellationToken cancellationToken)
    {
        if (queryPlan == null)
        {
            return BadRequest("No request body");
        }

        try
        {
            var result = await _mediator.Send(new SaveQueryPlanCommand
            {
                FacilityId = facilityId,
                QueryPlanResult = queryPlan,
                QueryPlanType = queryPlanType
            });

            return Accepted();
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(
                $"Error creating query plan for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}",
                "",
                facilityId,
                AuditEventType.Create,
                null);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating query plan for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}", 
                "", 
                facilityId, 
                AuditEventType.Create, 
                null);
            _logger.LogError(new EventId(LoggingIds.GenerateItems, "CreateQueryPlan"), ex, "An exception occurred while attempting to create a query place with a facility id of {id}", facilityId);
            throw;
        }
    }

    /// <summary>
    /// Updates a QueryPlanConfig record for a facilityId, queryPlanType, and queryPlan.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryPlanType"></param>
    /// <param name="queryPlan"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpPut("{queryPlanType}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateQueryPlan(
        string facilityId,
        QueryPlanType queryPlanType,
        [FromBody] IQueryPlan queryPlan,
        CancellationToken cancellationToken)
    {
        if (queryPlan == null)
        {
            return BadRequest("No request body");
        }

        try
        {
            var result = await _mediator.Send(new SaveQueryPlanCommand
            {
                FacilityId = facilityId,
                QueryPlanResult = queryPlan,
                QueryPlanType = queryPlanType
            });

            return Accepted();
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(
                $"Error creating query plan for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
                "",
                facilityId,
                AuditEventType.Update,
                null);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating query plan for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
                "",
                facilityId,
                AuditEventType.Update,
                null);
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "UpdateQueryPlan"), ex, "An exception occurred while attempting to update a query place with a facility id of {id}", facilityId);
            throw;
        }
    }

    /// <summary>
    /// Hard deletes a QueryPlanConfig for a given facilityId and queryPlanType.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryPlanType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("{queryPlanType}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteQueryPlan(
        string facilityId,
        QueryPlanType queryPlanType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("FacilityId is required.");
        }

        try
        {
            var result = await _mediator.Send(new DeleteQueryPlanCommand
            {
                FacilityId = facilityId
            });

            if (result == null)
            {
                return NotFound("No Config Found.");
            }

            return Accepted();
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating query plan for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
                "",
                facilityId,
                AuditEventType.Query,
                null);
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteQueryPlan"), ex, "An exception occurred while attempting to delete a query place with a facility id of {id}", facilityId);
            throw;
        }
    }

    private async Task SendAudit(string message, string correlationId, string facilityId, AuditEventType type, List<PropertyChangeModel> changes)
    {
        await _mediator.Send(new TriggerAuditEventCommand
        {
            AuditableEvent = new AuditEventMessage
            {
                FacilityId = facilityId,
                CorrelationId = "",
                Action = type,
                EventDate = DateTime.UtcNow,
                ServiceName = DataAcquisitionConstants.ServiceName,
                PropertyChanges = changes != null ? changes : new List<PropertyChangeModel>(),
                Resource = "DataAcquisition",
                User = "",
                UserId = "",
                Notes = $"{message}"
            }
        });
    }
}
