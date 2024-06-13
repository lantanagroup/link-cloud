using System.Net;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using static LantanaGroup.Link.DataAcquisition.Application.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/{facilityId}")]
[ApiController]
public class AuthenticationConfigController : Controller
{
    private readonly ILogger<AuthenticationConfigController> _logger;
    private readonly IMediator _mediator;
    private readonly CompareLogic _compareLogic;


    public AuthenticationConfigController(ILogger<AuthenticationConfigController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _compareLogic = new CompareLogic();
        _compareLogic.Config.MaxDifferences = 25;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    private async Task SendAudit(string message, string correlationId, string facilityId, AuditEventType type, List<PropertyChangeModel> changes)
    {
        await _mediator.Send(new TriggerAuditEventCommand
        {
            AuditableEvent = new AuditEventMessage
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Action = type,
                EventDate = DateTime.UtcNow,
                ServiceName = Application.Settings.DataAcquisitionConstants.ServiceName,
                PropertyChanges = changes != null ? changes : new List<PropertyChangeModel>(),
                Resource = "DataAcquisition",
                User = "",
                UserId = "",
                Notes = $"{message}"
                
            }
        });
    }

    /// <summary>
    /// Gets authentication settings for a given facilityId and config type.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryConfigurationTypePathParameter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{queryConfigurationTypePathParameter}/authentication")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthenticationConfiguration>> GetAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter? queryConfigurationTypePathParameter,
        CancellationToken cancellationToken)
    {
        try
        {
            if (queryConfigurationTypePathParameter == null)
            {
                throw new BadRequestException($"QueryConfigurationTypePathParameter is null.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException($"FacilityId is null.");
            }

            var result = await _mediator.Send(new GetAuthConfigQuery
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
            }, cancellationToken);

            if (result == null)
            {
                throw new NotFoundException("No Authentication Settings found.");
            }

            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetAuthenticationSettings"), ex, "An exception occurred while attempting to authentication settings with a facility id of {id}", facilityId);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
        
    }

    /// <summary>
    /// Creates a AuthenticationSettings for a facility.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryConfigurationTypePathParameter"></param>
    /// <param name="authenticationConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Facility Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost("{queryConfigurationTypePathParameter}/authentication")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AuthenticationConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthenticationConfiguration>> CreateAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter? queryConfigurationTypePathParameter,
        [FromBody] AuthenticationConfiguration authenticationConfiguration, 
        CancellationToken cancellationToken)
    {
        try
        {
            if (queryConfigurationTypePathParameter == null)
            {
                throw new BadRequestException($"QueryConfigurationTypePathParameter is null.");
            }

            if (authenticationConfiguration == null)
            {
                throw new BadRequestException($"AuthenticationConfiguration is null.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException($"FacilityId is null.");
            }

            var result = await _mediator.Send(new CreateAuthConfigCommand
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
                Configuration = authenticationConfiguration
            }, cancellationToken);

            if (result == null)
            {
                return Problem("AuthenticationConfiguration not created.", statusCode: (int)HttpStatusCode.InternalServerError);
            }

            await SendAudit($"Create authentication configuration for '{facilityId}'", null, facilityId,
                AuditEventType.Create, null);

            return CreatedAtAction(nameof(CreateAuthenticationSettings),
                new
                {
                    FacilityId = facilityId,
                    QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
                    AuthenticationConfiguration = authenticationConfiguration
                }, result);
        }
        catch (EntityAlreadyExistsException ex)
        {
            return Problem(title: "Entity Already Exists", detail: ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
        catch (BadRequestException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            await SendAudit($"Error creating authentication configuration for '{facilityId}'", null, facilityId, AuditEventType.Create, null);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Updates a AuthenticationSettings for a facility.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryConfigurationTypePathParameter"></param>
    /// <param name="authenticationConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpPut("{queryConfigurationTypePathParameter}/authentication")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter queryConfigurationTypePathParameter,
        [FromBody] AuthenticationConfiguration? authenticationConfiguration,
        CancellationToken cancellationToken)
    {
        try
        {
            if (queryConfigurationTypePathParameter == null)
            {
                throw new BadRequestException($"QueryConfigurationTypePathParameter is null.");
            }

            if (authenticationConfiguration == null)
            {
                throw new BadRequestException($"AuthenticationConfiguration is null.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException($"FacilityId is null.");
            }

            var existingAuthorizationConfiguration = await _mediator.Send(new GetAuthConfigQuery
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
            }, cancellationToken);

            var result = await _mediator.Send(new UpdateAuthConfigCommand
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
                Configuration = authenticationConfiguration
            }, cancellationToken);

            var resultChanges = _compareLogic.Compare(authenticationConfiguration, existingAuthorizationConfiguration);
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

            await SendAudit($"Update authentication configuration for '{facilityId}'", null, facilityId, AuditEventType.Update, propertyChanges);

            return Accepted(result);
        }
        catch (BadRequestException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            await SendAudit(
                $"Error creating authentication config for facility {facilityId}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}",
                "",
                facilityId,
                AuditEventType.Query,
                null);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryConfigurationTypePathParameter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("{queryConfigurationTypePathParameter}/authentication")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter? queryConfigurationTypePathParameter,
        CancellationToken cancellationToken)
    {
        try
        {
            if (queryConfigurationTypePathParameter == null)
            {
                throw new BadRequestException($"QueryConfigurationTypePathParameter is null.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException($"FacilityId is null.");
            }

            await _mediator.Send(new DeleteAuthConfigCommand
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter
            }, cancellationToken);

            await SendAudit($"Delete authentication configuration for facility {facilityId}", null, facilityId,
                AuditEventType.Delete, null);

            return Accepted();
        }
        catch (BadRequestException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            await SendAudit(ex.Message, null, facilityId, AuditEventType.Create, null); _logger.LogWarning(ex.Message);
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteAuthenticationSettings"), ex, "An exception occurred while attempting to delete authentication settings with a facility id of {id}", facilityId);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
