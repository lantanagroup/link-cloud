using LantanaGroup.Link.DataAcquisition.Application.Commands.Validate;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/connectionValidation")]
[ApiController]
public class ConnectionValidationController : Controller
{
    private readonly ILogger<ConnectionValidationController> _logger;
    private readonly IMediator _mediator;

    public ConnectionValidationController(ILogger<ConnectionValidationController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Validates the connection between the facility and the Link.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="patientId"></param>
    /// <param name="patientIdentifier"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("{facilityId}/$validate")]
    public async Task<IActionResult> ValidateFacilityConnection(
        string facilityId, 
        [FromQuery] string? patientId = default, 
        [FromQuery] string? patientIdentifier = default,
        CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(patientId) && string.IsNullOrWhiteSpace(patientIdentifier))
        {
            return BadRequest("No Patient ID or Patient Identifier was provided. One is required to validate.");
        }

        try
        {
            var result = await _mediator.Send(new ValidateFacilityConnectionQuery
            {
                FacilityId = facilityId,
                PatientId = patientId,
                PatientIdentifier = patientIdentifier
            }, cancellationToken);

            if(!result.IsConnected)
            {
                _logger.LogError("Connection validation failed for facility {FacilityId}", facilityId);
                return BadRequest("Connection validation failed.");
            }

            if(result.IsConnected && !result.IsPatientFound)
            {
                _logger.LogError("Patient not found for facility {FacilityId}", facilityId);
                return NotFound("Patient not found.");
            }

            if (result.IsConnected && result.IsPatientFound)
            {
                return Ok();
            }
        }
        catch (MissingFacilityIdException ex)
        {
            _logger.LogError(ex, "Facility ID is required to validate connection.");
            return BadRequest("Facility ID is required to validate connection.");
        }
        catch(MissingPatientIdOrPatientIdentifierException ex)
        {
            _logger.LogError(ex, "No Patient ID or Patient Identifier was provided. One is required to validate.");
            return BadRequest("No Patient ID or Patient Identifier was provided. One is required to validate.");
        }
        catch(FhirConnectionFailedException ex)
        {
            _logger.LogError(ex, "Error connecting to FHIR server for facility {FacilityId}", facilityId);
            return StatusCode(424, "An error occurred while connecting to the FHIR server. Please review your query connection configuration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating connection for facility {FacilityId}", facilityId);
            return StatusCode(500, "An error occurred while validating the connection.");
        }
        return StatusCode(500, "Something went wrong. Please contact an administrator.");
    }
}
