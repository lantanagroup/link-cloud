﻿using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Shared.Settings;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Create([FromBody] CensusConfigModel censusConfig)
    {
        if (string.IsNullOrWhiteSpace(censusConfig.FacilityId))
        {
            return BadRequest($"FacilityID is required.");
        }

        if (string.IsNullOrWhiteSpace(censusConfig.ScheduledTrigger))
        {
            return BadRequest("ScheduledTrigger is required.");
        }

        try
        {
            var entity = await _censusConfigManager.AddOrUpdateCensusConfig(censusConfig);

            return Created(entity.Id.ToString(), entity);
        }
        catch (MissingTenantConfigurationException ex)
        {
            _logger.LogError(ex.Message);
            
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.InsertItem, "Create Census Config"), ex, "An exception occurred while attempting to create an Census config with an id of {id}", censusConfig.FacilityId);
            throw;
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
            if (response == null)
                return NotFound();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Census Config"), ex, "An exception occurred while attempting to get a Census config with an id of {id}", facilityId);
            throw;
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
    public async Task<ActionResult<CensusConfigModel>> Put([FromBody] CensusConfigModel censusConfig, string facilityId)
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

        try
        {
            var existingEntity = await _censusConfigManager.GetCensusConfigByFacilityId(censusConfig.FacilityId);
            var entity = await _censusConfigManager.AddOrUpdateCensusConfig(censusConfig);
            if (existingEntity != null)
            {
                return Accepted(entity);
            }
            else
            {
                return Created(entity.Id.ToString(), entity);
            }
        }
        catch (MissingTenantConfigurationException ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update Census Config"), ex, "An exception occurred while attempting to update a Census config with an id of {id}", facilityId);
            throw;
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
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete Census Config"), ex, "An exception occurred while attempting to delete a Census config with an id of {id}", facilityId);
            throw;
        }
    }
}
