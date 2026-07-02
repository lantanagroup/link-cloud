using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/location-mappings")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class OrganizationLocationMappingController : Controller
{
    private readonly ILogger<OrganizationLocationMappingController> _logger;
    private readonly IOrganizationLocationMappingManager _manager;
    private readonly IOrganizationLocationMappingQueries _queries;

    public OrganizationLocationMappingController(
        ILogger<OrganizationLocationMappingController> logger,
        IOrganizationLocationMappingManager manager,
        IOrganizationLocationMappingQueries queries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    /// <summary>
    /// GET /api/location-mappings/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrganizationLocationMappingModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetByIdAsync(int id)
    {
        try
        {
            var result = await _queries.GetByIdAsync(id);

            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Sequence contains no elements", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "OrganizationLocationMapping with id {id} not found.", id);
            return Problem(title: "Not Found",
                           detail: $"OrganizationLocationMapping with id {id} not found.",
                           statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving OrganizationLocationMapping with id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /api/location-mappings/facility/{facilityId}
    /// Returns 200 OK with empty list [] when none exist
    /// </summary>
    [HttpGet("facility/{facilityId}")]
    [ProducesResponseType(typeof(List<OrganizationLocationMappingModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetByFacilityIdAsync(string facilityId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            facilityId = facilityId.SanitizeAndRemove();

            var result = await _queries.GetByFacilityIdAsync(facilityId);
            return Ok(result); // always 200, even if empty
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving OrganizationLocationMappings for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /api/location-mappings/facility/{facilityId}/search
    /// </summary>
    [HttpGet("facility/{facilityId}/search")]
    [ProducesResponseType(typeof(PagedConfigModel<OrganizationLocationMappingModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SearchAsync(
        string facilityId,
        [FromQuery] OrganizationLocationMappingSearchParameters searchParams,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            var search = new OrganizationLocationMappingSearchModel
            {
                FacilityId = facilityId.SanitizeAndRemove(),
                LocationId = searchParams.LocationId,
                LocationName = searchParams.LocationName,
                LocationAlias = searchParams.LocationAlias,
                PartOfValue = searchParams.PartOfValue,
                IsOrgLocation = searchParams.IsOrgLocation,
                IsActive = searchParams.IsActive
            };

            var result = await _queries.SearchAsync(search, pageNumber, pageSize);
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while searching OrganizationLocationMappings for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// PUT /api/location-mappings/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OrganizationLocationMappingModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateByIdAsync(int id, [FromBody] UpdateOrganizationLocationMappingModel model)
    {
        try
        {
            if (model == null)
                throw new BadRequestException("request body is required.");

            var updated = await _manager.UpdateByIdAsync(id, model);
            return Accepted(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "KeyNotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while updating OrganizationLocationMapping id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// DELETE /api/location-mappings/{id}
    /// Deletes the mapping with the specified Id
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteByFacilityIdAsync(int id)
    {
        try
        {
            await _manager.DeleteByIdAsync(id);
            return Accepted();
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while deleting OrganizationLocationMappings for Id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// DELETE /api/location-mappings/facility/{facilityId}
    /// Deletes ALL mappings for the facility
    /// </summary>
    [HttpDelete("facility/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteByFacilityIdAsync(string facilityId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            facilityId = facilityId.SanitizeAndRemove();

            await _manager.DeleteByFacilityIdAsync(facilityId);
            return Accepted();
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while deleting OrganizationLocationMappings for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}