using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/{facilityId}")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class AuthenticationConfigController : Controller
{
    private readonly ILogger<AuthenticationConfigController> _logger;
    private readonly IFhirQueryConfigurationManager _fhirQueryConfigurationManager;
    private readonly IFhirQueryConfigurationQueries _fhirQueryConfigurationQueries;
    private readonly IFhirQueryListConfigurationQueries _fhirQueryListConfigurationQueries;
    private readonly IFhirListQueryConfigurationManager _fhirQueryListConfigurationManager;


    public AuthenticationConfigController(ILogger<AuthenticationConfigController> logger, IFhirQueryConfigurationManager fhirQueryConfigurationManager, IFhirQueryConfigurationQueries fhirQueryConfigurationQueries, 
        IFhirListQueryConfigurationManager fhirQueryListConfigurationManager, IFhirQueryListConfigurationQueries fhirQueryListConfigurationQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager;
        _fhirQueryConfigurationQueries = fhirQueryConfigurationQueries;
        _fhirQueryConfigurationManager = fhirQueryConfigurationManager;
        _fhirQueryListConfigurationQueries = fhirQueryListConfigurationQueries;
    }

    /// <summary>
    /// Gets authentication settings for a given facilityId and config type.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="queryConfigurationTypePathParameter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Facility ID: 400
    ///     Missing Facility ID: 404
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{queryConfigurationTypePathParameter}/authentication")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthenticationConfigurationModel>> GetAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter? queryConfigurationTypePathParameter,
        CancellationToken cancellationToken)
    {
        try
        {
            facilityId = Sanitize(facilityId);
            
            if (queryConfigurationTypePathParameter == null)
            {
                throw new BadRequestException($"QueryConfigurationTypePathParameter is null.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException($"FacilityId is null.");
            }

            AuthenticationConfigurationModel? result;
            if (queryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
            {
                result = await _fhirQueryConfigurationQueries.GetAuthenticationConfigurationByFacilityId(facilityId, cancellationToken);
            }
            else
            {
                result = await _fhirQueryListConfigurationQueries.GetAuthenticationConfigurationByFacilityId(facilityId, cancellationToken);
            }

            if (result == null)
            {
                throw new NotFoundException("No Authentication Settings found.");
            }

            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetAuthenticationSettings"), ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetAuthenticationSettings"), ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetAuthenticationSettings"), ex, "MissingFacilityConfigurationException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.GetItem, "GetAuthenticationSettings"), ex, "An exception occurred while attempting to authentication settings with a facility id of {id}", facilityId.Sanitize());
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
    public async Task<ActionResult<AuthenticationConfigurationModel>> CreateAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter? queryConfigurationTypePathParameter,
        AuthenticationConfigurationModel authenticationConfiguration,
        CancellationToken cancellationToken)
    {
        try
        {
            facilityId = Sanitize(facilityId);

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

            if (ModelState.IsValid)
            {
                AuthenticationConfigurationModel? result;
                if (queryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
                {
                    result = await _fhirQueryConfigurationManager.CreateAuthenticationConfiguration(facilityId, authenticationConfiguration.ToDomain(), cancellationToken);
                }
                else
                {
                    result = await _fhirQueryListConfigurationManager.CreateAuthenticationConfiguration(facilityId, authenticationConfiguration.ToDomain(), cancellationToken);
                }

                return CreatedAtAction(nameof(CreateAuthenticationSettings),
                    new
                    {
                        FacilityId = facilityId,
                        QueryConfigurationTypePathParameter = queryConfigurationTypePathParameter,
                        AuthenticationConfiguration = authenticationConfiguration
                    }, result);
            }
            else
            {
                return BadRequest(ModelState);
            }
        }
        catch (EntityAlreadyExistsException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateAuthenticationSettings"), ex, "EntityAlreadyExistsException occurred.");
            return Problem(title: "Entity Already Exists", detail: ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateAuthenticationSettings"), ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateAuthenticationSettings"), ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateAuthenticationSettings"), ex, "MissingFacilityConfigurationException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateAuthenticationSettings"), ex, "An exception occurred while attempting to create authentication settings with a facility id of {id}", facilityId.Sanitize());
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
    ///     Bad Facility ID: 400
    ///     Missing Facility ID: 404
    ///     Server Error: 500
    /// </returns>
    [HttpPut("{queryConfigurationTypePathParameter}/authentication")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(AuthenticationConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateAuthenticationSettings(
        string facilityId,
        QueryConfigurationTypePathParameter queryConfigurationTypePathParameter,
        AuthenticationConfigurationModel? authenticationConfiguration,
        CancellationToken cancellationToken)
    {
        try
        {
            facilityId = Sanitize(facilityId);

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

            if (ModelState.IsValid)
            {
                AuthenticationConfigurationModel? result;
                if (queryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
                {
                    result = await _fhirQueryConfigurationManager.UpdateAuthenticationConfiguration(facilityId, authenticationConfiguration.ToDomain(), cancellationToken);
                }
                else
                {
                    result = await _fhirQueryListConfigurationManager.UpdateAuthenticationConfiguration(facilityId, authenticationConfiguration.ToDomain(), cancellationToken);
                }

                return Accepted(result);
            }
            else
            {
                return BadRequest(ModelState);
            }
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateAuthenticationSettings"), ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateAuthenticationSettings"), ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateAuthenticationSettings"), ex, "MissingFacilityConfigurationException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateAuthenticationSettings"), ex, "An exception occurred while attempting to update authentication settings with a facility id of {id}", facilityId.Sanitize());
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
            facilityId = Sanitize(facilityId);

            if (queryConfigurationTypePathParameter == null)
            {
                throw new BadRequestException($"QueryConfigurationTypePathParameter is null.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new BadRequestException($"FacilityId is null.");
            }

            if (queryConfigurationTypePathParameter == QueryConfigurationTypePathParameter.fhirQueryConfiguration)
            {
                await _fhirQueryConfigurationManager.DeleteAuthenticationConfiguration(facilityId, cancellationToken);
            }
            else
            {
                await _fhirQueryListConfigurationManager.DeleteAuthenticationConfiguration(facilityId, cancellationToken);
            }

            return Accepted();
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteAuthenticationSettings"), ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteAuthenticationSettings"), ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteAuthenticationSettings"), ex, "MissingFacilityConfigurationException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.DeleteItem, "DeleteAuthenticationSettings"), ex, "An exception occurred while attempting to delete authentication settings with a facility id of {id}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    private string Sanitize(string? input)
    {
        var inputSafe = HtmlInputSanitizer.SanitizeAndRemove(input);
        if (string.IsNullOrWhiteSpace(inputSafe))
            throw new BadRequestException("FacilityId is null.");
        return inputSafe;
    }
}
