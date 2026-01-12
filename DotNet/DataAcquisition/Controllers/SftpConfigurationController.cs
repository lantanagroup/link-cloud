using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class SftpConfigurationController : Controller
{
    private readonly ILogger<SftpConfigurationController> _logger;
    private readonly ISftpConfigurationManager _sftpConfigurationManager;
    private readonly ISftpConfigurationQueries _sftpConfigurationQueries;

    public SftpConfigurationController(
        ILogger<SftpConfigurationController> logger,
        ISftpConfigurationManager sftpConfigurationManager,
        ISftpConfigurationQueries sftpConfigurationQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sftpConfigurationManager = sftpConfigurationManager ?? throw new ArgumentNullException(nameof(sftpConfigurationManager));
        _sftpConfigurationQueries = sftpConfigurationQueries ?? throw new ArgumentNullException(nameof(sftpConfigurationQueries));
    }

    /// <summary>
    /// Gets an SftpConfiguration record by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the SFTP configuration.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Not Found: 404
    ///     Bad Request: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("sftpConfiguration/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> GetSftpConfigurationById(Guid id, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest("SftpConfiguration Id cannot be empty.");
            }

            var result = await _sftpConfigurationQueries.GetByIdAsync(id, cancellationToken);

            if (result is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for Id: {id}");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to get an SFTP configuration with Id: {id}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetSftpConfigurationById"), ex, "An exception occurred while attempting to get an SFTP configuration with Id: {Id}", id);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Gets an SftpConfiguration record for a given organization.
    /// </summary>
    /// <param name="organizationId">The organization identifier representing either a health care system or individual facility.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Not Found: 404
    ///     Bad Request: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{organizationId}/sftpConfiguration")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> GetOrgSftpConfiguration(string organizationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            organizationId = organizationId.SanitizeAndRemove();
            var result = await _sftpConfigurationQueries.GetByOrganizationIdAsync(organizationId, cancellationToken);

            if (result is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for organization: {organizationId}");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to get an SFTP configuration for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetOrgSftpConfiguration"), ex, "An exception occurred while attempting to get an SFTP configuration for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Creates an SftpConfiguration record for an organization. Should only be used for initial configuration.
    /// Supported Authentication Types: Basic (default)
    /// </summary>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="sftpConfiguration">The SFTP configuration model.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Organization Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost("{organizationId}/sftpConfiguration")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> CreateSftpConfiguration(string organizationId, SftpConfigurationModel? sftpConfiguration, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        try
        {
            if (sftpConfiguration is null)
            {
                return BadRequest("sftpConfiguration is null.");
            }

            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            // Default AuthType to Basic until other authentication options are supported
            sftpConfiguration.AuthenticationProtocol = AuthType.Basic;

            organizationId = organizationId.SanitizeAndRemove();
            var result =
                await _sftpConfigurationManager.CreateAsync(sftpConfiguration, organizationId, cancellationToken);

            return CreatedAtAction(nameof(GetOrgSftpConfiguration),
                new { organizationId },
                result);
        }
        catch (EntityAlreadyExistsException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateSftpConfiguration"), ex,
                "An attempt was made to create a duplicate SFTP configuration for organizationId: {OrganizationId}",
                organizationId);
            return Conflict($"An SftpConfiguration already exists for organization: {organizationId}. Use PUT endpoint to update it.");
        }
        catch (Exception ex)
        {
            var message =
                $"An unexpected error occurred while attempting to create an SFTP configuration for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.InsertItem, "CreateSftpConfiguration"), ex,
                "An exception occurred while attempting to create an SFTP configuration for organizationId: {OrganizationId}",
                organizationId);
            return Problem(title: "Internal Server Error", detail: message,
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Updates an SftpConfiguration record for an organization. This update will do a clean replace of the existing record.
    /// Supported Authentication Types: Basic (default)
    /// </summary>
    /// <param name="sftpConfiguration">The SFTP configuration model.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Not Modified: 304
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpPut("sftpConfiguration")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> UpdateSftpConfiguration(SftpConfigurationModel? sftpConfiguration, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        try
        {
            if (sftpConfiguration is null)
            {
                return BadRequest("sftpConfiguration is null.");
            }

            if (sftpConfiguration.Id == Guid.Empty)
            {
                return BadRequest("SftpConfiguration.Id cannot be empty.");
            }

            // Default AuthType to Basic as per requirements
            sftpConfiguration.AuthenticationProtocol = AuthType.Basic;

            var result = await _sftpConfigurationManager.UpdateAsync(sftpConfiguration, cancellationToken);

            if (result is null)
            {
                return Problem("SftpConfiguration not updated.", statusCode: (int)HttpStatusCode.NotModified);
            }

            return Accepted(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateSftpConfiguration"), ex,
                "An attempt was made to update a non-existent SFTP configuration with Id: {Id}",
                sftpConfiguration?.Id);
            return NotFound($"No {nameof(SftpConfiguration)} found for the provided Id: {sftpConfiguration?.Id}. Unable to update configuration.");
        }
        catch (Exception ex)
        {
            var message =
                $"An unexpected error occurred while attempting to update an SFTP configuration with Id: {sftpConfiguration?.Id}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "UpdateSftpConfiguration"), ex,
                "An exception occurred while attempting to update an SFTP configuration with Id: {Id}",
                sftpConfiguration?.Id);
            return Problem(title: "Internal Server Error", detail: message,
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Deletes an SftpConfiguration record for a given organization.
    /// </summary>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("{organizationId}/sftpConfiguration")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteSftpConfiguration(string organizationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            organizationId = organizationId.SanitizeAndRemove();
            var result = await _sftpConfigurationManager.DeleteAsync(organizationId, cancellationToken);

            return Accepted(result);
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to delete an SFTP configuration for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteSftpConfiguration"), ex, "An exception occurred while attempting to delete an SFTP configuration for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
