using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
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
        catch(Exception ex)
        {
            _logger.LogError($"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            return StatusCode(500);
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
            _logger.LogError($"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            return StatusCode(500);
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
            _logger.LogError($"Error encountered:\n{ex.Message}\n{ex.InnerException}");
            await SendAudit($"Error encountered:\n{ex.Message}\n{ex.InnerException}", null, facilityId, AuditEventType.Query);
            return StatusCode(500);
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
