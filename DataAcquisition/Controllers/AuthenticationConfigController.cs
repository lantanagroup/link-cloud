using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;

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
        QueryConfigurationTypePathParameter queryConfigurationTypePathParameter,
        CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            var result = await _mediator.Send(new GetAuthConfigQuery
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
            });

            if(result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch(Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
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
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter queryConfigurationTypePathParameter,
        [FromBody] AuthenticationConfiguration authenticationConfiguration, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        if (authenticationConfiguration == null)
        {
            return BadRequest("No request body");
        }

        try
        {
            var result = await _mediator.Send(new SaveAuthConfigCommand
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
                Configuration = authenticationConfiguration
            });

            if (result == null)
            {
                return NotFound();
            }

            await SendAudit($"Create authorization configuration  for '{facilityId}'", null, facilityId, AuditEventType.Create, null);
            
            return Accepted(result);
        }
        catch(MissingFacilityConfigurationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
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
    public async Task<IActionResult> UpdateAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter queryConfigurationTypePathParameter,
        [FromBody] AuthenticationConfiguration authenticationConfiguration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        if (authenticationConfiguration == null)
        {
            return BadRequest("No request body");
        }

        try
        {
            var existingAuthorizationConfiguration = await _mediator.Send(new GetAuthConfigQuery
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
            });



            var result = await _mediator.Send(new SaveAuthConfigCommand
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
                Configuration = authenticationConfiguration
            });

            if (result == null)
            {
                return NotFound();
            }
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
            await SendAudit($"Update authorization configuration for '{facilityId}'", null, facilityId, AuditEventType.Update, propertyChanges);

            return Accepted(result);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
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
        QueryConfigurationTypePathParameter queryConfigurationTypePathParameter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            var result = await _mediator.Send(new DeleteAuthConfigCommand
            {
                FacilityId = facilityId,
                QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
            });

            if (result == null)
            {
                return NotFound();
            }
            await SendAudit($"Delete authentication configuration for facility {facilityId}", null, facilityId, AuditEventType.Delete, null);
            
            return Accepted(result);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
