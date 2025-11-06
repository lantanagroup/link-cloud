using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
    private readonly IQueryPlanQueries _queryPlanQueries;

    public QueryPlanConfigController(ILogger<QueryPlanConfigController> logger, IQueryPlanManager queryPlanManager, IQueryPlanQueries queryPlanQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryPlanManager = queryPlanManager;
        _queryPlanQueries = queryPlanQueries;
    }

    /// <summary>
    /// Gets a QueryPlanConfig record for a given facilityId.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryParameters"></param>
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
        [FromQuery] GetQueryPlanParameters queryParameters,
        CancellationToken cancellationToken)
    {
        if(!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("parameter facilityId is required.");
            }

            if (queryParameters == null || queryParameters.Type == null)
            {
                throw new BadRequestException("type query parameter must be defined.");
            }

            var result = await _queryPlanQueries.GetAsync(facilityId, queryParameters.Type.Value, cancellationToken);

            if (result == null)
            {
                throw new NotFoundException(
                    $"No Query Plan found for facilityId: {facilityId}.");
            }

            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetQueryPlan"), ex, "An exception occurred while attempting to retrieve a query place with a facility id of {id}", facilityId.Sanitize());
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
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(QueryPlanModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQueryPlan(
        string facilityId, 
        [FromBody] QueryPlanApiModel? queryPlan,
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

            if(queryPlan.Type == null)
            {
                throw new BadRequestException("QueryPlan.Type is required.");
            }

            if (ModelState.IsValid)
            {
                var existing = await _queryPlanQueries.GetAsync(facilityId, queryPlan.Type.Value, cancellationToken);

                if (existing != null)
                {
                    throw new EntityAlreadyExistsException($"A Query Plan already exists for facilityId: {facilityId}.");
                }

                var result = await _queryPlanManager.AddAsync(new CreateQueryPlanModel
                {
                    EHRDescription = queryPlan.EHRDescription,
                    FacilityId = facilityId,    
                    InitialQueries = queryPlan.InitialQueries,
                    SupplementalQueries = queryPlan.SupplementalQueries,
                    PlanName = queryPlan.PlanName,
                    LookBack = queryPlan.LookBack,
                    Type = queryPlan.Type.Value
                }, cancellationToken);

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
            else 
            {
                return BadRequest(ModelState);
            }
        }
        catch(IncorrectQueryPlanOrderException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Incorrect Query Order", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (EntityAlreadyExistsException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Entity Already Exists", detail: ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch(ArgumentNullException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            string message = $"An exception occurred while attempting to create a QueryPlan for facility id of {facilityId.Sanitize()}.";
            _logger.LogError(ex, "An exception occurred while attempting to create a QueryPlan for facility id of {facilityId}.", facilityId.Sanitize());
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
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(QueryPlanModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateQueryPlan(
        string facilityId,
        [FromBody] QueryPlanApiModel? queryPlan,
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

            if (queryPlan.Type == null)
            {
                throw new BadRequestException("QueryPlan.Type is required.");
            }

            if (ModelState.IsValid)
            {
                var existing = await _queryPlanQueries.GetAsync(facilityId, queryPlan.Type.Value, cancellationToken);

                if (existing == null)
                {
                    throw new NotFoundException($"A Query Plan was not found for facilityId: {facilityId}.");
                }

                var result = await _queryPlanManager.UpdateAsync(new UpdateQueryPlanModel
                {
                    FacilityId = facilityId,
                    EHRDescription = queryPlan.EHRDescription,
                    InitialQueries = queryPlan.InitialQueries,
                    SupplementalQueries = queryPlan.SupplementalQueries,
                    LookBack = queryPlan.LookBack,
                    PlanName = queryPlan.PlanName,
                    Type = queryPlan.Type.Value
                },cancellationToken);

                return result != null ? Accepted(result) : Problem("QueryPlan not updated.", statusCode: (int)HttpStatusCode.InternalServerError); 
            }
            else
            {
                return BadRequest(ModelState);
            }
        }
        catch (IncorrectQueryPlanOrderException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Incorrect Query Order", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (BadRequestException ex)
        {
                
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            string message = $"An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.";
            _logger.LogError(ex, "An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.", facilityId.Sanitize());
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
        [FromQuery] DeleteQueryPlanParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("parameter facilityId is required.");
            }

            if (parameters == null || parameters.Type == null)
            {
                throw new BadRequestException("type query parameter must be defined.");
            }

            var existing = await _queryPlanQueries.GetAsync(facilityId.Sanitize(), parameters.Type.Value, cancellationToken);

            if (existing == null)
            {
                throw new NotFoundException($"A QueryPlan or Query component was not found for facilityId: {facilityId}.");
            }

            await _queryPlanManager.DeleteAsync(facilityId.Sanitize(), parameters.Type.Value, cancellationToken);

            return Accepted();
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "MissingFacilityConfigurationException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            string message = $"An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.";
            _logger.LogError(ex, "An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
