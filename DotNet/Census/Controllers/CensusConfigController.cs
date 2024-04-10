﻿using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Census.Controllers;

[Route("api/census/config/")]
[ApiController]
public class CensusConfigController : Controller
{
    private readonly ILogger<CensusConfigController> _logger;
    private readonly IMediator _mediator;

    public CensusConfigController(ILogger<CensusConfigController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Creates a CensusConfig for o given censusConfig
    /// </summary>
    /// <param name="censusConfig"></param>
    /// <returns></returns>
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
            await _mediator.Send(new CreateCensusConfigCommand
            {
                CensusConfigEntity = censusConfig
            });
            SendAudit("", censusConfig.FacilityId, AuditEventType.Create);
        }
        catch (MissingTenantConfigurationException ex)
        {
            _logger.LogError(ex.Message);
            SendAudit(string.Empty, censusConfig.FacilityId, AuditEventType.Create, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            SendAudit(string.Empty, censusConfig.FacilityId, AuditEventType.Create, $"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            return StatusCode(500);
        }

        return Accepted();
    }

    /// <summary>
    /// Returns the CensusConfig for a given facilityId
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns></returns>
    [HttpGet("{facilityId}")]
    public async Task<ActionResult<CensusConfigModel>> Get(string facilityId)
    {
        var response = await _mediator.Send(new GetCensusConfigQuery { FacilityId = facilityId });
        if (response == null)
            return NoContent();
        return Ok(response);
    }

    /// <summary>
    /// Updates a CensusConfig for a given censusConfigModel and facilityId
    /// </summary>
    /// <param name="censusConfig"></param>
    /// <param name="facilityId"></param>
    /// <returns></returns>
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

        CensusConfigModel configResponse = null;
        try
        {
            configResponse = await _mediator.Send(new UpdateCensusCommand
            {
                Config = censusConfig,
            });
            SendAudit("", facilityId, AuditEventType.Update);
        }
        catch (MissingTenantConfigurationException ex)
        {
            _logger.LogError(ex.Message);
            SendAudit(string.Empty, censusConfig.FacilityId, AuditEventType.Create, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            SendAudit(string.Empty, censusConfig.FacilityId, AuditEventType.Create, $"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            return StatusCode(500);
        }

        if (configResponse == null)
        {
            return NoContent();
        }

        return Ok(configResponse);
    }

    /// <summary>
    /// Deletes the CensusConfig for a given facilityId
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns></returns>
    [HttpDelete("{facilityId}")]
    public async Task<IActionResult> Delete(string facilityId)
    {
        CensusConfigModel configResponse = null;
        try
        {
            await _mediator.Send(new DeleteCensusConfigCommand { FacilityId = facilityId });
            SendAudit("", facilityId, AuditEventType.Delete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            SendAudit(string.Empty, facilityId, AuditEventType.Create, $"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            return StatusCode(500);
        }

        return Ok();
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    private void SendAudit(string correlationId, string facilityId, AuditEventType type, string? notes = null)
    {
        try
        {
            _mediator.Send(new TriggerAuditEventCommand
            {
                AuditableEvent = new AuditEventMessage
                {
                    FacilityId = facilityId,
                    CorrelationId = correlationId,
                    Action = type,
                    EventDate = DateTime.UtcNow,
                    ServiceName = CensusConstants.ServiceName,
                    //PropertyChanges = "example",
                    Resource = "CensusConfig",
                    User = "example",
                    UserId = "example",
                    Notes = notes ?? $"{type}: {facilityId}"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("There was an issue sending an audit.", ex);
        }
    }
}
