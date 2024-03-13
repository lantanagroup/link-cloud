using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/")]
[ApiController]
public class QueryConfigController : Controller
{
    private readonly ILogger<QueryConfigController> _logger;
    private readonly IMediator _mediator;
    private readonly CompareLogic _compareLogic;

    public QueryConfigController(ILogger<QueryConfigController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _compareLogic = new CompareLogic();
        _compareLogic.Config.MaxDifferences = 25;
    }

    /// <summary>
    /// Gets a FhirQueryConfiguration record for a given facilityId.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{facilityId}/fhirQueryConfiguration")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirQueryConfiguration>> GetFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            var result = await _mediator.Send(new GetFhirQueryConfigQuery
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
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [ApiExplorerSettings(IgnoreApi = true)]
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
                PropertyChanges = changes != null?changes: new List<PropertyChangeModel>(),
                Resource = "DataAcquisition",
                User = "",
                UserId = "",
                Notes = $"{message}"
                
            }
        });
    }

    /// <summary>
    /// Creates a FhirQueryConfiguration record for a facility. Should only be used for initial configuration.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="fhirQueryConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Facility Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost("fhirQueryConfiguration")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateFhirConfiguration([FromBody] FhirQueryConfiguration fhirQueryConfiguration, CancellationToken cancellationToken)
    {
        if (fhirQueryConfiguration == null)
        {
            return BadRequest("No request body");
        }

        try
        {
            var result = await _mediator.Send(new SaveFhirQueryConfigCommand
            {
                queryConfiguration = fhirQueryConfiguration
            });
            await SendAudit($"Create query configuration {fhirQueryConfiguration.Id} for '{fhirQueryConfiguration.FacilityId}'", null, fhirQueryConfiguration.FacilityId, AuditEventType.Create, null);

            return Accepted();
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(
                $"Error creating authentication config for facility {fhirQueryConfiguration.FacilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}",
                "",
                fhirQueryConfiguration.FacilityId,
                AuditEventType.Create,
                null);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating authentication config for facility {fhirQueryConfiguration.FacilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}",
                "",
                fhirQueryConfiguration.FacilityId,
                AuditEventType.Create,
                null);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Updates a FhirQueryConfiguration record for a facility. This update will do a clean replace of the existing record and will not update the delta between the 2 records.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="fhirQueryConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    /// <exception cref="NotImplementedException"></exception>
    [HttpPut("fhirQueryConfiguration")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateFhirConfiguration([FromBody] FhirQueryConfiguration fhirQueryConfiguration, CancellationToken cancellationToken)
    {
        if (fhirQueryConfiguration == null)
        {
            return BadRequest("No request body");
        }

        try
        {
            var existingFhirQueryConfiguration = await _mediator.Send(new GetFhirQueryConfigQuery
            {
                FacilityId = fhirQueryConfiguration.FacilityId
            }); ;


            var result = await _mediator.Send(new SaveFhirQueryConfigCommand
            {
                queryConfiguration = fhirQueryConfiguration
            });
          
            var resultChanges = _compareLogic.Compare(fhirQueryConfiguration, existingFhirQueryConfiguration);
            List<Difference> list = resultChanges.Differences;
            List<PropertyChangeModel> propertyChanges = new List<PropertyChangeModel>();
            list.ForEach(d => {
                propertyChanges.Add(new PropertyChangeModel
                {
                    PropertyName = d.PropertyName,
                    InitialPropertyValue = d.Object2Value,
                    NewPropertyValue = d.Object1Value
                });

            });

            await SendAudit($"Update query configuration {fhirQueryConfiguration.Id} for '{fhirQueryConfiguration.FacilityId}'", null, fhirQueryConfiguration.FacilityId, AuditEventType.Update, propertyChanges);
           
            return Accepted();
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(
                $"Error creating authentication config for facility {fhirQueryConfiguration.FacilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
                "",
                fhirQueryConfiguration.FacilityId,
                AuditEventType.Update,
                null);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating authentication config for facility {fhirQueryConfiguration.FacilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
                "",
                fhirQueryConfiguration.FacilityId,
                AuditEventType.Update,
                null);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Hard deletes a FhirQueryConfiguration record for a given facilityId.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    /// <exception cref="NotImplementedException"></exception>
    [HttpDelete("{facilityId}/fhirQueryConfiguration")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            var result = await _mediator.Send(new DeleteFhirQueryConfigurationCommand
            {
                FacilityId = facilityId
            });

            if (result == null)
            {
                return NotFound();
            }
            await SendAudit($"Delete query configuration for facility {facilityId}", null, facilityId, AuditEventType.Delete, null);
            return Accepted(result);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
