using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/connectionValidation")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class ConnectionValidationController : Controller
{
    private readonly ILogger<ConnectionValidationController> _logger;
    private readonly IValidateFacilityConnectionService _validateFacilityConnectionService;

    public ConnectionValidationController(ILogger<ConnectionValidationController> logger, IValidateFacilityConnectionService validateFacilityConnectionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validateFacilityConnectionService = validateFacilityConnectionService ?? throw new ArgumentNullException(nameof(validateFacilityConnectionService));
    }

    /// <summary>
    /// Validates the connection between the facility and the Link.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="patientId"></param>
    /// <param name="patientIdentifier"></param>
    /// <param name="measureId"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     MissingFacilityId: 400
    ///     Missing Patient Information: 400
    ///     Missing Measure ID: 400
    ///     Missing Start Date: 400
    ///     Missing End Date: 400
    ///     Fhir Endpoint Connection Error: 424
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{facilityId}/$validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FacilityConnectionResult>> ValidateFacilityConnection(
        string facilityId, 
        [FromQuery] string? patientId = default, 
        [FromQuery] string? patientIdentifier = default,
        [FromQuery] string? measureId = default,
        [FromQuery] DateTime? start = default,
        [FromQuery] DateTime? end = default,
        CancellationToken cancellationToken = default)
    {
        if(!ConnectionValidationRequestValidator.ValidateRequest(facilityId, patientId, patientIdentifier, measureId, start, end, out var errorMessage))
        {
            return Problem(errorMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await _validateFacilityConnectionService.ValidateConnection(new ValidateFacilityConnectionRequest
            {
                FacilityId = facilityId,
                PatientId = patientId,
                PatientIdentifier = patientIdentifier,
                MeasureId = measureId,
                Start = start.Value,
                End = end.Value
            }, cancellationToken);

            if(!result.IsConnected)
            {
                return Problem(result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest);
            }

            if(result.IsConnected && !result.IsPatientFound)
            {
                return Problem("Patient not found for provided facilityId." + (string.IsNullOrEmpty(result.ErrorMessage) ? "" : " Error Message: " + result.ErrorMessage), statusCode: StatusCodes.Status404NotFound);
            }

            if (result.IsConnected && result.IsPatientFound)
            {
                return Ok(result);
            }
        }
        catch (MissingFacilityIdException ex)
        {
            return Problem("Facility ID is required to validate connection.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            return Problem($"No configuration found for Facility ID {facilityId}.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (MissingPatientIdOrPatientIdentifierException ex)
        {
            return Problem("No Patient ID or Patient Identifier was provided. One is required to validate.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch(FhirConnectionFailedException ex)
        {
            return Problem($"An error occurred while connecting to the FHIR server. Please review your query connection configuration.\nerrorMessage: {ex.Message}\ninnerException:\n{ex.InnerException}", statusCode: StatusCodes.Status424FailedDependency);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "ValidateFacilityConnection"), ex, "An exception occurred in ValidateFacilityConnection");
            return Problem($"An error occurred while validating the connection.\nerror:\n{ex.Message}\nInnerException:\n{ex.InnerException}", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Problem("Something went wrong. Please contact an administrator.", statusCode: StatusCodes.Status500InternalServerError);
    }
}
