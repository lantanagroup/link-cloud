using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/{facilityId}")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
public class QueryPlanConfigController : Controller
{
    private readonly ILogger<QueryPlanConfigController> _logger;
    private readonly IQueryPlanManager _queryPlanManager;

    public QueryPlanConfigController(ILogger<QueryPlanConfigController> logger, IQueryPlanManager queryPlanManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryPlanManager = queryPlanManager ?? throw new ArgumentNullException(nameof(queryPlanManager));
    }

    /// <summary>
    /// Gets a QueryPlanConfig record for a given facilityId.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("QueryPlan")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(QueryPlan))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetQueryPlan(
        string facilityId,
        Frequency type,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("parameter facilityId is required.");
            }

            var result = await _queryPlanManager.GetAsync(facilityId, type, cancellationToken);

            if (result == null)
            {
                throw new NotFoundException(
                    $"No Query Plan found for facilityId: {facilityId}.");
            }

            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetQueryPlan"), ex, "An exception occurred while attempting to retrieve a query place with a facility id of {id}", facilityId);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Creates a QueryPlanConfig for a facility
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryPlan"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Facility Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost("QueryPlan")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(QueryPlan))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQueryPlan(
        string facilityId,
        [FromBody] QueryPlan? queryPlan,
        CancellationToken cancellationToken)
    {
        try
        {
            if (queryPlan == null)
            {
                throw new BadRequestException("request body is null");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("facilityId is required.");
            }

            var existing = await _queryPlanManager.GetAsync(facilityId, queryPlan.Type, cancellationToken);

            if (existing != null) 
            {
                throw new EntityAlreadyExistsException($"A Query Plan already exists for facilityId: {facilityId}.");
            }

            var result = await _queryPlanManager.AddAsync(queryPlan, cancellationToken);

            if (result == null)
            {
                return Problem("QueryPlan not created.", statusCode: (int)HttpStatusCode.InternalServerError);
            }

            return CreatedAtAction(nameof(CreateQueryPlan),
                new
                {
                    FacilityId = facilityId,
                    QueryPlan = result
                }, result);
        }
        catch (EntityAlreadyExistsException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Entity Already Exists", detail: ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            string message = $"An exception occurred while attempting to create a QueryPlan for facility id of {facilityId}.";
            _logger.LogError(ex, message, facilityId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
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
    [HttpPut("QueryPlan")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(QueryPlan))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateQueryPlan(
        string facilityId,
        QueryPlan? queryPlan,
        CancellationToken cancellationToken)
    {
        try
        {
            if (queryPlan == null)
            {
                throw new BadRequestException("request body is null");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("parameter facilityId is required.");
            }

            var existing = await _queryPlanManager.GetAsync(facilityId, queryPlan.Type, cancellationToken);

            if (existing == null)
            {
                throw new NotFoundException($"A Query Plan was not found for facilityId: {facilityId}.");
            }

            var result = await _queryPlanManager.UpdateAsync(queryPlan, cancellationToken);

            return result != null ? Accepted(result) : Problem("QueryPlan not updated.", statusCode: (int)HttpStatusCode.InternalServerError);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            string message = $"An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.";
            _logger.LogError(ex, message, facilityId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Hard deletes a QueryPlanConfig for a given facilityId and queryPlanType.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("QueryPlan")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteQueryPlan(
        string facilityId,
        Frequency type,
        CancellationToken cancellationToken)
    {

        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("parameter facilityId is required.");
            }

            var existing = await _queryPlanManager.GetAsync(facilityId, type, cancellationToken);
            if (existing == null)
            {
                throw new NotFoundException($"A QueryPlan or Query component was not found for facilityId: {facilityId}.");
            }

            await _queryPlanManager.DeleteAsync(facilityId, cancellationToken);

            return Accepted();
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            string message = $"An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.";
            _logger.LogError(ex, message, facilityId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
