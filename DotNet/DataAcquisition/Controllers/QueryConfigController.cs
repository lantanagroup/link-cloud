using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Policies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static LantanaGroup.Link.DataAcquisition.Application.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirQueryConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirQueryConfiguration>> GetFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("GetFhirQueryConfigQuery.FacilityId is null or empty.");
            }

            var result = await _mediator.Send(new GetFhirQueryConfigQuery
            {
                FacilityId = facilityId
            }, cancellationToken);

            if (result == null)
            {
                throw new NotFoundException($"No {nameof(FhirQueryConfiguration)} found for facilityId: {facilityId}");
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
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetFhirQueryConfig"), ex, "An exception occurred while attempting to get a fhir configuration with a facility id of {id}", facilityId);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
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
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FhirQueryConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirQueryConfiguration>> CreateFhirConfiguration([FromBody] FhirQueryConfiguration? fhirQueryConfiguration, CancellationToken cancellationToken)
    {
        string? facilityId = fhirQueryConfiguration?.FacilityId;
        try
        {
            if (fhirQueryConfiguration == null)
            {
                throw new BadRequestException("fhirQueryConfiguration is null.");
            }

            facilityId = fhirQueryConfiguration.FacilityId;

            FhirQueryConfiguration? existing = null;
            try
            {
                existing = await _mediator.Send(new GetFhirQueryConfigQuery
                {
                    FacilityId = facilityId
                }, cancellationToken);
            }
            catch (NotFoundException ex)
            {
                //We will hopefully NOT find an existing item and hit this catch
            }

            if (existing != null)
            {
                throw new EntityAlreadyExistsException(
                    $"A FhirQueryConfiguration already exists for facilityId: {facilityId}. Use PUT endpoint to update it.");
            }

            var result = await _mediator.Send(new SaveFhirQueryConfigCommand
            {
                queryConfiguration = fhirQueryConfiguration
            }, cancellationToken);

            if (result == null)
            {
                return Problem("FhirQueryConfiguration not created.", statusCode: (int)HttpStatusCode.InternalServerError);
            }

            return CreatedAtAction(nameof(CreateFhirConfiguration),
                new
                {
                    FacilityId = facilityId,
                    FhirQueryConfiguration = fhirQueryConfiguration
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
            string message =
                $"An exception occurred while attempting to get a FhirQueryConfiguration with a facility id of {facilityId}. " + Environment.NewLine + ex.Message;
            _logger.LogError(new EventId(LoggingIds.InsertItem, "CreateFhirConfiguration"), ex, message, facilityId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
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
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateFhirConfiguration([FromBody] FhirQueryConfiguration? fhirQueryConfiguration, CancellationToken cancellationToken)
    {
        string? facilityId = fhirQueryConfiguration?.FacilityId;
        try
        {
            if (fhirQueryConfiguration == null)
            {
                throw new BadRequestException("fhirQueryConfiguration is null.");
            }

            var existingFhirQueryConfiguration = await _mediator.Send(new GetFhirQueryConfigQuery
            {
                FacilityId = facilityId
            }, cancellationToken);

            var result = await _mediator.Send(new SaveFhirQueryConfigCommand
            {
                queryConfiguration = fhirQueryConfiguration
            }, cancellationToken);

            if (result == null)
            {
                return Problem("FhirQueryConfiguration not Updated.", statusCode: (int)HttpStatusCode.NotModified);
            }

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

            return Accepted(result);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(ex.Message + Environment.NewLine + ex.StackTrace);
            return BadRequest(ex.Message);
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
            string message =
                $"An exception occurred while attempting to update a fhir query configuration with a facility id of {facilityId}. " + Environment.NewLine + ex.Message;
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "UpdateFhirConfiguration"), ex, message, facilityId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
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
    public async Task<ActionResult> DeleteFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException("GetFhirQueryConfigQuery.FacilityId is null or empty.");
            }

            var result = await _mediator.Send(new DeleteFhirQueryConfigurationCommand
            {
                FacilityId = facilityId
            });

            return Accepted(result);
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
            string message =
                $"An exception occurred while attempting to delete a fhir query configuration with a facility id of {facilityId}. " + Environment.NewLine + ex.Message;
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteFhirConfiguration"), ex, message, facilityId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
