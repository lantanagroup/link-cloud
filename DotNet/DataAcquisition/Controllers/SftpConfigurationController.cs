using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using System.Net;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Services;
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
    private readonly ISftpCredentialService _sftpCredentialService;
    private readonly ITenantApiService _tenantApiService;
    private readonly IValidator<CreateSftpConfigurationModel> _createValidator;
    private readonly IValidator<SftpConfigurationModel> _updateValidator;

    public SftpConfigurationController(
        ILogger<SftpConfigurationController> logger,
        ISftpConfigurationManager sftpConfigurationManager,
        ISftpConfigurationQueries sftpConfigurationQueries,
        ISftpCredentialService sftpCredentialService,
        ITenantApiService tenantApiService,
        IValidator<CreateSftpConfigurationModel> createValidator,
        IValidator<SftpConfigurationModel> updateValidator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sftpConfigurationManager = sftpConfigurationManager ?? throw new ArgumentNullException(nameof(sftpConfigurationManager));
        _sftpConfigurationQueries = sftpConfigurationQueries ?? throw new ArgumentNullException(nameof(sftpConfigurationQueries));
        _sftpCredentialService = sftpCredentialService ?? throw new ArgumentNullException(nameof(sftpCredentialService));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
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
    [HttpGet("sftp-configurations/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> GetSftpConfigurationById(Guid id, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

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
    [HttpGet("{organizationId}/sftp-configurations")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> GetOrgSftpConfiguration(string organizationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            var result = await _sftpConfigurationQueries.GetByOrganizationIdAsync(organizationId, cancellationToken);

            if (result is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for organization: {organizationId.SanitizeAndRemove()}");
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
    /// Optionally accepts credentials which will be stored securely in a configured secret manager.
    /// Supported Authentication Types: Basic (default)
    /// </summary>
    /// <param name="organizationId">The identifier for the reporting organization.</param>
    /// <param name="sftpConfiguration">The SFTP configuration model with optional credentials.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Organization Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost("{organizationId}/sftp-configurations")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> CreateSftpConfiguration(string organizationId, CreateSftpConfigurationModel? sftpConfiguration, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

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

            // Validate the request
            var validationResult = await _createValidator.ValidateAsync(sftpConfiguration, cancellationToken);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }

            // Verify that the facility/organization exists
            var facilityExists = await _tenantApiService.CheckFacilityExists(organizationId, cancellationToken);

            if (!facilityExists)
            {
                return NotFound($"No facility found for organizationId: {organizationId}");
            }

            // Default AuthType to Basic until other authentication options are supported
            sftpConfiguration.AuthenticationProtocol = AuthType.Basic;

            // Create the configuration in the database
            var result =
                await _sftpConfigurationManager.CreateAsync(sftpConfiguration, organizationId, cancellationToken);

            // If credentials were provided, store them in the configured secret manager
            if (sftpConfiguration.Credentials is not null &&
                !string.IsNullOrWhiteSpace(sftpConfiguration.Credentials.Username) &&
                !string.IsNullOrWhiteSpace(sftpConfiguration.Credentials.Password))
            {
                var credentialResult = await _sftpCredentialService.SetCredentialsAsync(
                    organizationId,
                    sftpConfiguration.Credentials,
                    cancellationToken);

                if (!credentialResult)
                {
                    _logger.LogWarning(
                        new EventId(LoggingIds.InsertItem, "CreateSftpConfiguration"),
                        "SFTP configuration created but credentials could not be stored for organizationId: {OrganizationId}",
                        organizationId);
                }
            }

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
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.InsertItem, "CreateSftpConfiguration"), ex,
                "An attempt was made to create an SFTP configuration for organizationId: {OrganizationId} which already has a FHIR List configuration",
                organizationId);
            return Conflict(ex.Message);
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
    /// <param name="organizationId">The identifier for the reporting organization.</param>
    /// <param name="configurationId">The identifier of the SFTP configuration.</param>
    /// <param name="sftpConfiguration">The SFTP configuration model.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Not Modified: 304
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpPut("{organizationId}/sftp-configurations/{configurationId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(SftpConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConfigurationModel>> UpdateSftpConfiguration(string organizationId, string configurationId, SftpConfigurationModel? sftpConfiguration, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            if (sftpConfiguration is null)
            {
                return BadRequest("sftpConfiguration is null.");
            }

            if (sftpConfiguration.Id == Guid.Empty)
            {
                return BadRequest("SftpConfiguration.Id cannot be empty.");
            }

            if (!Guid.TryParse(configurationId, out var configId) || configId != sftpConfiguration.Id)
            {
                return BadRequest("The Id in the request does not match the Id in the SftpConfiguration model.");
            }

            // Validate the request
            var validationResult = await _updateValidator.ValidateAsync(sftpConfiguration, cancellationToken);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }

            organizationId = organizationId.SanitizeAndRemove();

            var result = await _sftpConfigurationManager.UpdateAsync(organizationId, sftpConfiguration, cancellationToken);

            if (result is null)
            {
                return Problem("SftpConfiguration not updated.", statusCode: (int)HttpStatusCode.NotModified);
            }

            return Accepted(result);
        }
        catch (OrganizationalAccessException ex)
        {
            _logger.LogWarning(
                new EventId(LoggingIds.UpdateItem, "UpdateSftpConfiguration"),
                ex,
                "The organizationId: {OrganizationId} does not have access to SFTP configuration Id: {ConfigurationId}",
                organizationId, configurationId.SanitizeAndRemove());
            return BadRequest($"The organizationId: {organizationId} does not have access to SFTP configuration Id: {configurationId.SanitizeAndRemove()}");
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(new EventId(LoggingIds.UpdateItem, "UpdateSftpConfiguration"), ex,
                "An attempt was made to update a non-existent SFTP configuration with Id: {Id}",
                sftpConfiguration?.Id.ToString().SanitizeAndRemove());
            return NotFound($"No {nameof(SftpConfiguration)} found for the provided Id: {sftpConfiguration?.Id.ToString().SanitizeAndRemove()}. Unable to update configuration.");
        }
        catch (Exception ex)
        {
            var message =
                $"An unexpected error occurred while attempting to update an SFTP configuration with Id: {sftpConfiguration?.Id}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "UpdateSftpConfiguration"), ex,
                "An exception occurred while attempting to update an SFTP configuration with Id: {Id}",
                sftpConfiguration?.Id.ToString().SanitizeAndRemove());
            return Problem(title: "Internal Server Error", detail: message,
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Deletes an SftpConfiguration record for a given organization.
    /// </summary>
    /// <param name="organizationId">The identifier for the reporting organization</param>
    /// <param name="configurationId">The identifier for the SFTP configuration</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("{organizationId}/sftp-configurations/{configurationId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteSftpConfiguration(string organizationId, string configurationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            if (!Guid.TryParse(configurationId, out var configId))
            {
                return BadRequest("The SFTP configuration Id is in an invalid format.");
            }

            // Delete the configuration from the database
            try
            {
                var result = await _sftpConfigurationManager.DeleteAsync(organizationId, configId, cancellationToken);

                // Also delete credentials from Key Vault (if they exist)
                try
                {
                    await _sftpCredentialService.DeleteCredentialsAsync(configurationId, cancellationToken);
                }
                catch (Exception credEx)
                {
                    // Log but don't fail the request if credential deletion fails
                    _logger.LogWarning(
                        new EventId(LoggingIds.DeleteItem, "DeleteSftpConfiguration"),
                        credEx,
                        "SFTP configuration deleted but credentials could not be removed for organizationId: {OrganizationId}",
                        organizationId);
                }

                return Accepted(result);
            }
            catch (OrganizationalAccessException ex)
            {
                _logger.LogWarning(
                    new EventId(LoggingIds.DeleteItem, "DeleteSftpConfiguration"),
                    ex,
                    "The organizationId: {OrganizationId} does not have access to SFTP configuration Id: {ConfigurationId}",
                    organizationId, configurationId.SanitizeAndRemove());
                return BadRequest($"The organizationId: {organizationId} does not have access to SFTP configuration Id: {configurationId.SanitizeAndRemove()}");
            }
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to delete an SFTP configuration for organizationId: {configurationId.SanitizeAndRemove()}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteSftpConfiguration"), ex, "An exception occurred while attempting to delete an SFTP configuration for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    #region Credential Endpoints

    /// <summary>
    /// Updates SFTP credentials for an organization's configuration.
    /// Credentials are stored securely in a configured secret manager.
    /// </summary>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="credentials">The credentials to store.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpPut("{organizationId}/sftp-configurations/credentials")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateSftpCredentials(string organizationId, SftpCredentialsModel? credentials, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            if (credentials is null)
            {
                return BadRequest("Credentials are required.");
            }

            if (string.IsNullOrWhiteSpace(credentials.Username))
            {
                return BadRequest("Username is required.");
            }

            if (string.IsNullOrWhiteSpace(credentials.Password))
            {
                return BadRequest("Password is required.");
            }

            // Verify the SFTP configuration exists
            var existingConfig = await _sftpConfigurationQueries.GetByOrganizationIdAsync(organizationId, cancellationToken);
            if (existingConfig is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for organization: {organizationId}. Create a configuration first.");
            }

            var result = await _sftpCredentialService.SetCredentialsAsync(organizationId, credentials, cancellationToken);

            if (!result)
            {
                return Problem("Failed to store credentials.", statusCode: (int)HttpStatusCode.InternalServerError);
            }

            return Accepted();
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to update SFTP credentials for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "UpdateSftpCredentials"), ex, "An exception occurred while attempting to update SFTP credentials for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Gets the credential status for an organization's SFTP configuration.
    /// Returns whether credentials exist, without exposing the actual values.
    /// </summary>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{organizationId}/sftp-configurations/credentials/status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpCredentialStatusModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpCredentialStatusModel>> GetSftpCredentialStatus(string organizationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            // Verify the SFTP configuration exists
            var existingConfig = await _sftpConfigurationQueries.GetByOrganizationIdAsync(organizationId, cancellationToken);
            if (existingConfig is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for organization: {organizationId}");
            }

            var result = await _sftpCredentialService.GetCredentialStatusAsync(organizationId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to get SFTP credential status for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetSftpCredentialStatus"), ex, "An exception occurred while attempting to get SFTP credential status for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Deletes SFTP credentials for an organization.
    /// The SFTP configuration itself is not deleted.
    /// </summary>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("{organizationId}/sftp-configurations/credentials")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteSftpCredentials(string organizationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // validate user access to organization before proceeding - future enhancement

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            // Verify the SFTP configuration exists
            var existingConfig = await _sftpConfigurationQueries.GetByOrganizationIdAsync(organizationId, cancellationToken);
            if (existingConfig is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for organization: {organizationId}");
            }

            var result = await _sftpCredentialService.DeleteCredentialsAsync(organizationId, cancellationToken);
            return Accepted(result);
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while attempting to delete SFTP credentials for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteSftpCredentials"), ex, "An exception occurred while attempting to delete SFTP credentials for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    #endregion

    #region Connection Test

    /// <summary>
    /// Tests the SFTP connection for an organization's configuration.
    /// Verifies connectivity to the SFTP server and optionally checks if the remote directory is accessible.
    /// </summary>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Request: 400
    ///     Not Found: 404
    ///     Server Error: 500
    /// </returns>
    [HttpPost("{organizationId}/sftp-configurations/test-connection")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpConnectionTestResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpConnectionTestResult>> TestSftpConnection(string organizationId, CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;

        // Sanitize organizationId
        organizationId = organizationId.SanitizeAndRemove();

        try
        {
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                return BadRequest("OrganizationId is null or empty.");
            }

            // Get the SFTP configuration
            var config = await _sftpConfigurationQueries.GetByOrganizationIdAsync(organizationId, cancellationToken);
            if (config is null)
            {
                return NotFound($"No {nameof(SftpConfiguration)} found for organization: {organizationId}");
            }

            // Get credentials, assume there will never be an anonymous connection for SFTP
            var credentials = await _sftpCredentialService.GetCredentialsAsync(organizationId, cancellationToken);
            if (credentials is null || string.IsNullOrWhiteSpace(credentials.Username) || string.IsNullOrWhiteSpace(credentials.Password))
            {
                return BadRequest("No credentials configured for this SFTP configuration. Please set credentials before testing the connection.");
            }

            // Test the connection
            try
            {
                using var client = new SftpClient(config.Host, config.Port, credentials.Username, credentials.Password);
                client.ConnectionInfo.Timeout = config.Timeout;

                await client.ConnectAsync(cancellationToken);

                var result = new SftpConnectionTestResult
                {
                    Success = client.IsConnected,
                    Message = "Successfully connected to SFTP server."
                };

                // Verify directory access if configured
                if (client.IsConnected && !string.IsNullOrWhiteSpace(config.RemoteDirectory))
                {
                    if (await client.ExistsAsync(config.RemoteDirectory, cancellationToken))
                    {
                        result.Message += $" Remote directory '{config.RemoteDirectory}' is accessible.";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"Connected to SFTP server but remote directory '{config.RemoteDirectory}' does not exist or is not accessible.";
                    }
                }

                client.Disconnect();

                _logger.LogInformation(
                    new EventId(LoggingIds.GetItem, "TestSftpConnection"),
                    "SFTP connection test for organizationId: {OrganizationId} - Success: {Success}",
                    organizationId, result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    new EventId(LoggingIds.GetItem, "TestSftpConnection"),
                    ex,
                    "SFTP connection test failed for organizationId: {OrganizationId}",
                    organizationId);

                return Ok(new SftpConnectionTestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                });
            }
        }
        catch (Exception ex)
        {
            var message = $"An unexpected error occurred while testing SFTP connection for organizationId: {organizationId}. Trace Id: {httpContext.TraceIdentifier}";
            _logger.LogError(new EventId(LoggingIds.GetItem, "TestSftpConnection"), ex, "An exception occurred while testing SFTP connection for organizationId: {OrganizationId}", organizationId);
            return Problem(title: "Internal Server Error", detail: message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    #endregion
}
