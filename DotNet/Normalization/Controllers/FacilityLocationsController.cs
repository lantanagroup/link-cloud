using LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers;

[Route("api/normalization/facility-locations")]
[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
public class FacilityLocationsController : ControllerBase
{
    private readonly IFacilityLocationManager _facilityLocationManager;

    public FacilityLocationsController(IFacilityLocationManager facilityLocationManager)
    {
        _facilityLocationManager = facilityLocationManager;
    }

    [HttpGet("facilities/{facilityId}/locations/{locationId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityLocationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FacilityLocationModel>> Get(string facilityId, string locationId, CancellationToken cancellationToken)
    {
        facilityId = SanitizeIdentifier(facilityId);
        locationId = SanitizeIdentifier(locationId);
        var validationError = Validate(facilityId, locationId, null);
        if (validationError != null)
        {
            return Problem(detail: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var facilityLocation = await _facilityLocationManager.Get(facilityId, locationId, cancellationToken);
            return facilityLocation == null
                ? Problem(detail: "The requested facility location does not exist.", statusCode: StatusCodes.Status404NotFound)
                : Ok(facilityLocation);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("facilities/{facilityId}/locations")]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityLocationModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FacilityLocationModel>> Post(
        string facilityId,
        FacilityLocationPostModel model,
        CancellationToken cancellationToken)
    {
        facilityId = SanitizeIdentifier(facilityId);
        var sanitizedModel = Sanitize(model);
        var validationError = Validate(facilityId, sanitizedModel.LocationId, sanitizedModel.PartOfId);
        if (validationError != null)
        {
            return Problem(detail: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var facilityLocation = await _facilityLocationManager.Create(facilityId, sanitizedModel, cancellationToken);
            return CreatedAtAction(
                nameof(Get),
                new { facilityId = facilityLocation.FacilityId, locationId = facilityLocation.LocationId },
                facilityLocation);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static FacilityLocationPostModel Sanitize(FacilityLocationPostModel model) => new()
    {
        LocationId = SanitizeIdentifier(model.LocationId),
        PartOfId = model.PartOfId == null ? null : SanitizeIdentifier(model.PartOfId),
        LocationName = model.LocationName?.Sanitize(),
        LocationAlias = model.LocationAlias?.Sanitize()
    };

    private static string SanitizeIdentifier(string? identifier) => identifier.Sanitize().Trim();

    private static string? Validate(string facilityId, string locationId, string? partOfId) =>
        string.IsNullOrWhiteSpace(facilityId)
            ? "A facility identifier must be provided."
            : string.IsNullOrWhiteSpace(locationId)
                ? "A location identifier must be provided."
                : partOfId == locationId
                    ? "A facility location cannot be its own parent."
                    : null;
}