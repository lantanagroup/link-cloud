using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class QueryListController : Controller
{
    private readonly ILogger<QueryConfigController> _logger;
    private readonly IFhirQueryListConfigurationManager _fhirQueryListConfigurationManager;

    public QueryListController(ILogger<QueryConfigController> logger, IFhirQueryListConfigurationManager fhirQueryListConfigurationManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager;
    }

    [HttpGet("{facilityId}/fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfiguration>> GetFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            var result = await _fhirQueryListConfigurationManager.SingleOrDefaultAsync(q => q.FacilityId == facilityId, cancellationToken);

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetFhirConfiguration"), ex, "An exception occurred while attempting to get a fhir query configuration with a facility id of {id}", HtmlInputSanitizer.Sanitize(facilityId));
            throw;
        }
    }

    /// <summary>
    /// Creates a FhirQueryConfiguration record for a given facilityId.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="fhirListConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfiguration>> PostFhirConfiguration(FhirListConfiguration fhirListConfiguration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fhirListConfiguration.FacilityId))
        {
            return BadRequest("No facility id provided.");
        }

        if (string.IsNullOrWhiteSpace(fhirListConfiguration.FhirBaseServerUrl) || !Uri.IsWellFormedUriString(fhirListConfiguration.FhirBaseServerUrl, UriKind.Absolute))
        {
            return BadRequest("Invalid FHIR Base Server URL.");
        }

        try
        {
            var entity = await _fhirQueryListConfigurationManager.AddAsync(fhirListConfiguration, cancellationToken);

            return Ok(entity);
        }
        catch (EntityAlreadyExistsException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GenerateItems, "PostFhirConfiguration"), ex, "An exception occurred while attempting to create a fhir query configuration with a facility id of {id}", HtmlInputSanitizer.Sanitize(fhirListConfiguration.FacilityId));
            throw;
        }
    }

    /// <summary>
    /// Updates a FhirQueryConfiguration record for a given facilityId.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="fhirListConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut("fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfiguration>> PutFhirConfiguration(FhirListConfiguration fhirListConfiguration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fhirListConfiguration.FacilityId))
        {
            return BadRequest("No facility id provided.");
        }

        if (string.IsNullOrWhiteSpace(fhirListConfiguration.FhirBaseServerUrl) || !Uri.IsWellFormedUriString(fhirListConfiguration.FhirBaseServerUrl, UriKind.Absolute))
        {
            return BadRequest("Invalid FHIR Base Server URL.");
        }

        try
        {
            var entity = await _fhirQueryListConfigurationManager.UpdateAsync(fhirListConfiguration, cancellationToken);

            return Ok(entity);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.UpdateItem, "PutFhirConfiguration"), ex, "An exception occurred while attempting to update a fhir query configuration with a facility id of {id}", HtmlInputSanitizer.Sanitize(fhirListConfiguration.FacilityId));
            throw;
        }
    }

    /// <summary>
    /// Deletes a FhirQueryConfiguration record for a given facilityId.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="fhirListConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete("{facilityId}/fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        var sanitizedFacilityId = HtmlInputSanitizer.Sanitize(facilityId);

        try
        {
            var entity = await _fhirQueryListConfigurationManager.DeleteAsync(sanitizedFacilityId, cancellationToken);

            return Accepted();
        }
        catch (MissingFacilityConfigurationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.DeleteItem, "DeleteFhirConfiguration"), ex, "An exception occurred while attempting to delete a fhir query list configuration with a facility id of {id}", sanitizedFacilityId);
            throw;
        }
    }
}
