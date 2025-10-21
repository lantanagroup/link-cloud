using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Models.Exceptions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/{facilityId}")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
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
        [Required] string facilityId,
        [FromQuery] GetQueryPlanParameters queryParameters,
        CancellationToken cancellationToken)
    {
        facilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            ModelState.AddModelError(nameof(facilityId), "parameter facilityId is required.");
            return ValidationProblem(
                title: "Bad Request",
                type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                detail: "One or more parameters were invalid.",
                statusCode: (int)HttpStatusCode.BadRequest,
                modelStateDictionary: ModelState);
        }

        try
        {
            var result = await _queryPlanQueries.GetAsync(facilityId, queryParameters.Type.Value, cancellationToken);

            if (result == null)
            {
                throw new NotFoundException(
                    $"No Query Plan found for facilityId: {facilityId}.");
            }

            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetQueryPlan"), ex,
                "Unexpected exception occurred for facility id of {facilityId}.", facilityId);

            return Problem(title: "Internal Server Error",
                detail: $"An exception occurred while attempting to get a QueryPlan for facility id of {facilityId}.",
                statusCode: (int)HttpStatusCode.InternalServerError);
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
        [Required] string facilityId,
        [Required, FromBody] QueryPlanApiModel? queryPlan,
        CancellationToken cancellationToken)
    {
        try
        {
            facilityId = facilityId.SanitizeAndRemove();
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                ModelState.AddModelError(nameof(facilityId), "parameter facilityId is required.");
                return ValidationProblem(
                    title: "Bad Request",
                    type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                    detail: "One or more parameters were invalid.",
                    statusCode: (int)HttpStatusCode.BadRequest,
                    modelStateDictionary: ModelState);
            }

            var exists = await _queryPlanQueries.ExistsAsync(facilityId, queryPlan.Type.Value, cancellationToken);

            if (exists)
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

            return CreatedAtAction(nameof(GetQueryPlan),
                new
                {
                    FacilityId = facilityId
                }, result);
        }
        catch (IncorrectQueryPlanOrderException ex)
        {
            _logger.LogError(ex, "IncorrectQueryPlanOrderException occurred.");
            return Problem(title: "Incorrect Query Order", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (EntityAlreadyExistsException ex)
        {
            _logger.LogError(ex, "EntityAlreadyExistsException occurred.");
            return Problem(title: "Entity Already Exists", detail: ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title:"Bad Request", detail:ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
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
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "ArgumentNullException occurred.");
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
        [Required] string facilityId,
        [Required,FromBody] QueryPlanApiModel? queryPlan,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(queryPlan?.FacilityId) &&
                !string.Equals(facilityId, queryPlan.FacilityId, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(queryPlan.FacilityId),
                    "facilityId in body must match route facilityId.");
            }

            facilityId = facilityId.SanitizeAndRemove();
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                ModelState.AddModelError(nameof(facilityId), "parameter facilityId is required.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(
                    title: "Bad Request",
                    type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                    detail: "One or more parameters were invalid.",
                    statusCode: (int)HttpStatusCode.BadRequest,
                    modelStateDictionary: ModelState);
            }

            var exists = await _queryPlanQueries.ExistsAsync(facilityId, queryPlan.Type.Value, cancellationToken);

            if (!exists)
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
            }, cancellationToken);

            return result != null ? Accepted(result) : Problem("QueryPlan not updated.", statusCode: (int)HttpStatusCode.InternalServerError);
        }
        catch (IncorrectQueryPlanOrderException ex)
        {
            _logger.LogError(ex, "IncorrectQueryPlanOrderException occurred.");
            return Problem(title: "Incorrect Query Order", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
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
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "ArgumentNullException occurred.");
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
    /// <param name="parameters"></param>
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
        [Required] string facilityId,
        [Required,FromQuery] DeleteQueryPlanParameters parameters,
        CancellationToken cancellationToken)
    {
        facilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            ModelState.AddModelError(nameof(facilityId), "parameter facilityId is required.");
            return ValidationProblem(
                title: "Bad Request",
                type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                detail: "One or more parameters were invalid.", 
                statusCode: (int)HttpStatusCode.BadRequest,
                modelStateDictionary: ModelState);
        }

        try
        {
            var exists = await _queryPlanQueries.ExistsAsync(facilityId, parameters.Type.Value, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException($"A QueryPlan or Query component was not found for facilityId: {facilityId}.");
            }

            await _queryPlanManager.DeleteAsync(facilityId, parameters.Type.Value, cancellationToken);

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

    /// <summary>
    /// Hard deletes all QueryPlanConfigs for a given facilityId.
    /// </summary>
    /// <param name="facilityId">The ID of the facility whose query plans will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    ///     Success: 202 Accepted
    ///     Bad Facility ID: 400 Bad Request
    ///     Server Error: 500 Internal Server Error
    /// </returns>
    [HttpDelete("QueryPlan/All")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteAllQueryPlans(
        [Required] string facilityId,
        CancellationToken cancellationToken)
    {
        facilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            ModelState.AddModelError(nameof(facilityId), "parameter facilityId is required.");
            return ValidationProblem(
                title: "Bad Request",
                type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                detail: "One or more parameters were invalid.",
                statusCode: (int)HttpStatusCode.BadRequest,
                modelStateDictionary: ModelState);
        }

        try
        {

            // Call the manager/service method that deletes all query plans
            await _queryPlanManager.DeleteAllQueryPlansAsync(facilityId, cancellationToken);

            return Accepted(); // 202
        }
        catch (Exception ex)
        {
            string message = $"An error occurred while deleting all QueryPlans for facilityId '{facilityId}'.";
            _logger.LogError(ex, "An exception occurred while attempting to update a QueryPlan for facility id of {facilityId}.", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
