﻿using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Census.Controllers;

[Route("api/census/{facilityId}")]
[ApiController]
public class CensusController : Controller
{
    private readonly ILogger<CensusController> _logger;
    private readonly IMediator _mediator;

    public CensusController(ILogger<CensusController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Gets Patient List history for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns></returns>
    [HttpGet("history")]
    public async Task<ActionResult<List<PatientCensusHistoricEntity>>> GetCensusHistory(string facilityId)
    {
        try
        {
            var history = await _mediator.Send(new GetCensusHistoryQuery
            {
                FacilityId = facilityId
            });
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Census History"), ex, "An exception occurred while attempting to get census history with an id of {id}", facilityId);
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            throw;
        }
    }

    /// <summary>
    /// Gets the admitted patients for a facility within a date range. If no dates are provided, it will return all active patients.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    [HttpGet("history/admitted")]
    public async Task<ActionResult<List<CensusPatientListEntity>>> GetAdmittedPatients(string facilityId, DateTime startDate = default, DateTime endDate = default)
    {
        try
        {
            var patients = await _mediator.Send(new GetAdmittedPatientsQuery
            {
                FacilityId = facilityId,
                StartDate = startDate,
                EndDate = endDate
            });
            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Admitted Patients"), ex, "An exception occurred while attempting to get admitted patients with an id of {id}", facilityId);
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            throw;
        }
    }   

    /// <summary>
    /// Gets the current census for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns></returns>
    [HttpGet("current")]
    public async Task<ActionResult<List<CensusPatientListEntity>>> GetCurrentCensus(string facilityId)
    {
        try
        {
            var patients = await _mediator.Send(new GetCurrentCensusQuery
            {
                FacilityId = facilityId
            });
            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Current Census"), ex, "An exception occurred while attempting to get current census with an id of {id}", facilityId);
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            throw;
        }
    }

    /// <summary>
    /// Gets all patient list records for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns></returns>
    [HttpGet("all")]
    public async Task<ActionResult<List<CensusPatientListEntity>>> GetAllPatientsForFacility(string facilityId)
    {
        try
        {
            var patients = await _mediator.Send(new GetAllPatientsForFacilityQuery
            {
                FacilityId = facilityId
            });
            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get All Patients For Facility"), ex, "An exception occurred while attempting to get All Patients For Facility with an id of {id}", facilityId);
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            throw;
        }
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    private async Task SendAudit(string message, string correlationId, string facilityId, AuditEventType type)
    {
        await _mediator.Send(new TriggerAuditEventCommand
        {
            AuditableEvent = new AuditEventMessage
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Action = type,
                EventDate = DateTime.UtcNow,
                ServiceName = CensusConstants.ServiceName,
                //PropertyChanges = "example",
                Resource = "Census",
                User = "example",
                UserId = "example",
                Notes = $"{type}: {facilityId}\n{message}"
            }
        });
    }
}
