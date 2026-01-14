using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Census.Domain.Managers;
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

    public CensusConfigController(ILogger<CensusConfigController> logger, ICensusConfigManager censusConfigManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _censusConfigManager = censusConfigManager ?? throw new ArgumentNullException(nameof(censusConfigManager));
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
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CensusConfigEntity))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPost]
    public async Task<IActionResult> Create(CensusConfigModel censusConfig)
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
            var entity =  CensusConfigModel.FromDomain(await _censusConfigManager.AddOrUpdateCensusConfig(censusConfig));

            return Created(entity.FacilityId, entity);
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
            var response = await _censusConfigManager.GetCensusConfigByFacilityId(facilityId);
            if (response is null)
                return NotFound();

            CensusConfigModel model = new CensusConfigModel
            {
                FacilityId = response.FacilityID,
                ScheduledTrigger = response.ScheduledTrigger,
                Enabled = response.Enabled
            };
            return Ok(model);
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
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(CensusConfigEntity))]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CensusConfigEntity))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpPut("{facilityId}")]
    public async Task<ActionResult<CensusConfigModel>> Put(CensusConfigModel censusConfig, string facilityId)
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
            var existingEntity = await _censusConfigManager.GetCensusConfigByFacilityId(censusConfig.FacilityId);
            var entity = await _censusConfigManager.AddOrUpdateCensusConfig(censusConfig);
            if (existingEntity != null)
            {
                return Accepted(CensusConfigModel.FromDomain(entity));
            }
            else
            {
                return Created(entity.Id.ToString(), CensusConfigModel.FromDomain(entity));
            }
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
            await _censusConfigManager.DeleteCensusConfigByFacilityId(facilityId);

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