using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Census.Controllers;

[Route("api/census/config/")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class CensusConfigController : Controller
{
    private readonly ILogger<CensusConfigController> _logger;
    private readonly ICensusConfigManager _censusConfigManager;
    private readonly ICensusConfigQueries _censusConfigQueries;

    public CensusConfigController(ILogger<CensusConfigController> logger, ICensusConfigManager censusConfigManager, ICensusConfigQueries censusConfigQueries)
    {
        _logger = logger;
        _censusConfigManager = censusConfigManager;
        _censusConfigQueries = censusConfigQueries;
    }

    /// <summary>
    /// Creates a CensusConfig for o given censusConfig
    /// </summary>
    /// <param name="censusConfig"></param>
    /// <returns>
    ///     Created: 201
    ///     Bad Request: 400
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CensusConfigModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPost]
    public async Task<IActionResult> Create(CensusConfigApiModel censusConfig)
    {
        if (string.IsNullOrWhiteSpace(censusConfig.FacilityId))
        {
            return BadRequest($"FacilityID is required.");
        }

        if (string.IsNullOrWhiteSpace(censusConfig.ScheduledTrigger))
        {
            return BadRequest("ScheduledTrigger is required.");
        }

        if (!CronExpression.IsValidExpression(censusConfig.ScheduledTrigger))
        {
            return BadRequest("ScheduledTrigger is not a valid cron expression.");
        }

        try
        {
            var existingEntity = await _censusConfigQueries.GetAsync(censusConfig.FacilityId, HttpContext.RequestAborted);

            if (existingEntity != null)
            {
                return BadRequest($"Census Config already exists for Facility {censusConfig.FacilityId.Sanitize()}");
            }

            var entity = await _censusConfigManager.CreateAsync(new CreateCensusConfigModel
            {
                FacilityId = censusConfig.FacilityId,
                ScheduledTrigger = censusConfig.ScheduledTrigger,
            });

            return Created(entity.Id.ToString(), entity);
        }
        catch (MissingTenantConfigurationException ex)
        {
            return Problem(
                detail: "No Facility for the provided FacilityId was found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusConfigController.Create");
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Returns the CensusConfig for a given facilityId
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns>
    ///     Success: 200
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CensusConfigModel))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet("{facilityId}")]
    public async Task<ActionResult<CensusConfigModel>> Get(string facilityId)
    {
        try
        {
            var result = await _censusConfigQueries.GetAsync(facilityId, HttpContext.RequestAborted);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusConfigController.Get");
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Updates a CensusConfig for a given censusConfigModel and facilityId
    /// </summary>
    /// <param name="censusConfig"></param>
    /// <param name="facilityId"></param>
    /// <returns>
    ///     Created: 201
    ///     Accepted: 202
    ///     Bad Scheduled Trigger: 400
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(CensusConfigModel))]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CensusConfigModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPut("{facilityId}")]
    public async Task<ActionResult<CensusConfigModel>> Put(CensusConfigApiModel censusConfig, string facilityId)
    {
        if (string.IsNullOrWhiteSpace(censusConfig.FacilityId))
        {
            return BadRequest($"FacilityID is required.");
        }

        if (string.IsNullOrWhiteSpace(censusConfig.ScheduledTrigger))
        {
            return BadRequest("ScheduledTrigger is required.");
        }

        if (!string.Equals(facilityId, censusConfig.FacilityId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest($"FacilityID in request path does not match facility in request body.");
        }

        if (!CronExpression.IsValidExpression(censusConfig.ScheduledTrigger))
        {
            return BadRequest("ScheduledTrigger is not a valid cron expression.");
        }

        try
        {
            var existingEntity = await _censusConfigQueries.GetAsync(censusConfig.FacilityId, HttpContext.RequestAborted);

            if (existingEntity == null)
            {
                return BadRequest($"Census Config does not exist for Facility {facilityId.Sanitize()}");
            }

            var entity = await _censusConfigManager.UpdateAsync(new UpdateCensusConfigModel
            {
                FacilityId = censusConfig.FacilityId,
                ScheduledTrigger = censusConfig.ScheduledTrigger
            }, HttpContext.RequestAborted);

            return Accepted(entity);
        }
        catch (MissingTenantConfigurationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusConfigController.Put");
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Deletes the CensusConfig for a given facilityId
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns>
    ///     No Content: 204
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpDelete("{facilityId}")]
    public async Task<IActionResult> Delete(string facilityId)
    {
        try
        {
            await _censusConfigManager.DeleteAsync(facilityId, HttpContext.RequestAborted);

            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusConfigController.Delete");
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}