using Azure;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

/// <summary>
/// Manages the generic OAuth client-credentials configuration for a facility whose EHR vendor is
/// "Other". The configuration applies to the facility's patient-query configuration only.
/// </summary>
[Route("api/data-acquisition/facilities/{facilityId}/fhir-authentication-configuration")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class FhirAuthenticationConfigurationController : ControllerBase
{
    private readonly IFhirAuthenticationConfigurationService _fhirAuthenticationConfigurationService;
    private readonly ILogger<FhirAuthenticationConfigurationController> _logger;

    public FhirAuthenticationConfigurationController(
        IFhirAuthenticationConfigurationService fhirAuthenticationConfigurationService,
        ILogger<FhirAuthenticationConfigurationController> logger)
    {
        _fhirAuthenticationConfigurationService = fhirAuthenticationConfigurationService
            ?? throw new ArgumentNullException(nameof(fhirAuthenticationConfigurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the facility's generic OAuth configuration.
    /// </summary>
    /// <remarks>
    /// The client secret is never returned. <c>clientSecretStored</c> reports whether one is held in
    /// the secret manager.
    /// </remarks>
    /// <param name="facilityId">The facility whose configuration to read.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <returns>The configuration, or a Problem result when the facility has none.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirAuthenticationConfigurationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetFhirAuthenticationConfiguration(string facilityId,
                                                                        CancellationToken cancellationToken)
    {
        try
        {
            var facilityIdSafe = Validated(facilityId);

            var result = await _fhirAuthenticationConfigurationService.GetAsync(facilityIdSafe, cancellationToken);

            if (result == null)
            {
                return Problem(title: "Not Found",
                               detail: "No FHIR authentication configuration exists for this facility.",
                               statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex, "Bad request reading the FHIR authentication configuration for facility " +
                                   "{FacilityId}", facilityId.SanitizeForLog());
            return Problem(title: "Bad Request", detail: ex.Message.Sanitize(),
                           statusCode: StatusCodes.Status400BadRequest);
        }
        catch (OperationCanceledException)
        {
            // Request is being cancelled
            throw;
        }
        catch (RequestFailedException ex)
        {
            return SecretManagerUnavailable(ex, facilityId);
        }
        catch (Exception ex)
        {
            return UnexpectedFailure(ex, facilityId, "reading");
        }
    }

    /// <summary>
    /// Creates or replaces the facility's generic OAuth configuration.
    /// </summary>
    /// <remarks>
    /// The client secret is write-only: its value is stored in the secret manager and never returned.
    /// Omit it to keep the secret already stored. This replaces whatever authentication the facility
    /// currently uses, including Epic or Basic.
    /// </remarks>
    /// <param name="facilityId">The facility whose configuration to replace.</param>
    /// <param name="request">The configuration to store.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <returns>The stored configuration, or a Problem result.</returns>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(FhirAuthenticationConfigurationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateOrUpdateFhirAuthenticationConfiguration(
        string facilityId,
        [FromBody] FhirAuthenticationConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var facilityIdSafe = Validated(facilityId);

            var result = await _fhirAuthenticationConfigurationService.CreateOrUpdateAsync(facilityIdSafe,
                                                                                           request,
                                                                                           cancellationToken);

            return Accepted(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex, "Bad request storing the FHIR authentication configuration for facility " +
                                   "{FacilityId}", facilityId.SanitizeForLog());
            return Problem(title: "Bad Request", detail: ex.Message.Sanitize(),
                           statusCode: StatusCodes.Status400BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "No FHIR query configuration exists for facility {FacilityId}",
                               facilityId.SanitizeForLog());
            return Problem(title: "Not Found", detail: ex.Message.Sanitize(),
                           statusCode: StatusCodes.Status404NotFound);
        }
        catch (OperationCanceledException)
        {
            // Request is being cancelled
            throw;
        }
        catch (RequestFailedException ex)
        {
            return SecretManagerUnavailable(ex, facilityId);
        }
        catch (Exception ex)
        {
            return UnexpectedFailure(ex, facilityId, "storing");
        }
    }

    /// <summary>
    /// Validates the facility id rather than repairing it. <c>SanitizeAndRemove</c> strips characters
    /// instead of failing, so "fac!ility@1" becomes "facility1" - on an endpoint that reads and
    /// overwrites credentials, that would serve or overwrite a different facility than the caller
    /// named. See docs/other-vendor-oauth-configuration.md.
    /// </summary>
    private static string Validated(string? facilityId)
    {
        var sanitized = HtmlInputSanitizer.SanitizeAndRemove(facilityId ?? string.Empty);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new BadRequestException("FacilityId is required.");
        }

        if (!string.Equals(sanitized, facilityId, StringComparison.Ordinal))
        {
            throw new BadRequestException("FacilityId contains characters that are not allowed.");
        }

        return sanitized;
    }

    /// <summary>
    /// The secret manager is an upstream dependency, so a fault reaching us is an availability
    /// problem rather than a defect in this request.
    /// </summary>
    private ObjectResult SecretManagerUnavailable(RequestFailedException ex, string facilityId)
    {
        _logger.LogError(ex, "The secret manager returned {Status} for facility {FacilityId}",
                         ex.Status, facilityId.SanitizeForLog());

        return Problem(title: "Service Unavailable",
                       detail: "The secret manager is unavailable. Trace Id: " +
                               $"{HttpContext.TraceIdentifier}",
                       statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// The exception message is deliberately withheld: a secret manager fault can name the vault and
    /// the secret. The trace id is what ties the response to the logged detail.
    /// </summary>
    private ObjectResult UnexpectedFailure(Exception ex, string facilityId, string action)
    {
        _logger.LogError(ex, "An exception occurred while {Action} the FHIR authentication configuration " +
                             "for facility {FacilityId}", action, facilityId.SanitizeForLog());

        return Problem(title: "Internal Server Error",
                       detail: $"An unexpected error occurred. Trace Id: {HttpContext.TraceIdentifier}",
                       statusCode: StatusCodes.Status500InternalServerError);
    }
}
