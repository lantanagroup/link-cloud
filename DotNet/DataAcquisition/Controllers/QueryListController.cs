using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static LantanaGroup.Link.DataAcquisition.Domain.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class QueryListController : Controller
{
    private readonly ILogger<QueryListController> _logger;
    private readonly IFhirListQueryConfigurationManager _fhirQueryListConfigurationManager;
    private readonly IFhirQueryListConfigurationQueries _fhirQueryListConfigurationQueries;
    private readonly IEhrPatientListService _ehrPatientListService;

    //add api settings
    private readonly ApiSettings _apiSettings;

    public QueryListController(ILogger<QueryListController> logger, IFhirListQueryConfigurationManager fhirQueryListConfigurationManager, IFhirQueryListConfigurationQueries fhirQueryListConfigurationQueries, IEhrPatientListService ehrPatientListService, IOptions<ApiSettings> apiSettings)
    {
        _logger = logger;
        _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager;
        _apiSettings = apiSettings?.Value ?? throw new ArgumentNullException(nameof(apiSettings));
        _fhirQueryListConfigurationQueries = fhirQueryListConfigurationQueries;
        _ehrPatientListService = ehrPatientListService ?? throw new ArgumentNullException(nameof(ehrPatientListService));
    }

    /// <summary>
    /// Gets the FhirQueryList configuration for a given facilityId.
    /// </summary>
    /// <param name="facilityId">The facility whose configuration should be returned.</param>
    /// <param name="includePatients">
    /// When true, includes the patients that are present on each of the ehrPatientLists. Defaults to false.
    /// Each configured list is read from the facility's EHR, so enabling this adds one EHR round trip per list.
    /// If any list cannot be read the whole request fails; no partially populated configuration is returned.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("{facilityId}/fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfigurationModel>> GetFhirConfiguration(string facilityId, [FromQuery] bool includePatients = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("A facility id is required.");
        }

        var sanitizedFacilityId = HtmlInputSanitizer.Sanitize(facilityId);

        try
        {
            var result = await _fhirQueryListConfigurationQueries.GetByFacilityIdAsync(sanitizedFacilityId, cancellationToken);

            if (result == null)
            {
                return NotFound();
            }

            if (includePatients)
            {
                await _ehrPatientListService.PopulatePatientsAsync(result, cancellationToken);
            }

            return Ok(result);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (TooManyRequestsException ex)
        {
            Response.Headers["Retry-After"] = ((int)Math.Ceiling(ex.RetryAfter.TotalSeconds)).ToString();
            return Problem(
                $"The EHR rate limited the patient list requests for facility {sanitizedFacilityId}. Retry later or omit includePatients.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (PatientListRetrievalFailedException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status424FailedDependency);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "GetFhirConfiguration"), ex, "An exception occurred while attempting to get a fhir query configuration with a facility id of {id}", sanitizedFacilityId);
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfigurationModel>> PostFhirConfiguration(FhirListConfigurationModel fhirListConfiguration, CancellationToken cancellationToken)
    {
        fhirListConfiguration.Validate(ModelState);

        if (ModelState.IsValid)
        {

            try
            {
                var model = await _fhirQueryListConfigurationManager.CreateAsync(new CreateFhirListConfigurationModel
                {
                    Authentication = fhirListConfiguration.Authentication,
                    EHRPatientLists = fhirListConfiguration.EHRPatientLists,
                    FacilityId = fhirListConfiguration.FacilityId,
                    FhirBaseServerUrl = fhirListConfiguration.FhirBaseServerUrl
                }, cancellationToken);

                return Ok(model);
            }
            catch (EntityAlreadyExistsException ex)
            {
                return Conflict(ex.Message);
            }
            catch (MissingFacilityConfigurationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "PostFhirConfiguration"), ex, "An exception occurred while attempting to create a fhir query configuration with a facility id of {id}", HtmlInputSanitizer.Sanitize(fhirListConfiguration.FacilityId));
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request. Please try again later\n{ex.Message}");
            }
        }
        else
        {
            return BadRequest(ModelState);
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfigurationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfigurationModel>> PutFhirConfiguration(FhirListConfigurationModel fhirListConfiguration, CancellationToken cancellationToken)
    {
        fhirListConfiguration.Validate(ModelState);

        if (ModelState.IsValid)
        {
            try
            {
                var model = await _fhirQueryListConfigurationManager.UpdateAsync(new UpdateFhirListConfigurationModel
                {
                    Id = fhirListConfiguration.Id,
                    FacilityId = fhirListConfiguration.FacilityId,
                    FhirBaseServerUrl = fhirListConfiguration.FhirBaseServerUrl,
                    Authentication = fhirListConfiguration.Authentication,
                    EHRPatientLists = fhirListConfiguration.EHRPatientLists
                }, cancellationToken);

                return Ok(model);
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
        else
        {
            return BadRequest(ModelState);
        }
    }

    /// <summary>
    /// Deletes a FhirQueryConfiguration record for a given facilityId.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete("{facilityId}/fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest("facilityId is null or empty.");
        }

        var sanitizedFacilityId = HtmlInputSanitizer.Sanitize(facilityId);

        try
        {
            var entity = await _fhirQueryListConfigurationQueries.GetByFacilityIdAsync(facilityId, cancellationToken);

            if (entity == null)
            {
                return NotFound("No Fhir List Configuration Found for FacilityId: " + facilityId.SanitizeAndRemove());
            }
            var deleted = await _fhirQueryListConfigurationManager.DeleteAsync(sanitizedFacilityId, cancellationToken);

            return Ok(deleted);
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
