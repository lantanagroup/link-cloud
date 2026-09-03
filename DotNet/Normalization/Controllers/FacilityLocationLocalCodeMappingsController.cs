using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers;

[Route("api/normalization/hsloc-mappings")]
[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
public class FacilityLocationLocalCodeMappingsController : ControllerBase
{
    private readonly IFacilityLocationLocalCodeMappingManager _mappingManager;
    private readonly IFacilityLocationLocalCodeMappingQueries _mappingQueries;

    public FacilityLocationLocalCodeMappingsController(
        IFacilityLocationLocalCodeMappingManager mappingManager,
        IFacilityLocationLocalCodeMappingQueries mappingQueries)
    {
        _mappingManager = mappingManager;
        _mappingQueries = mappingQueries;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityLocationLocalCodeMappingModel>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> GetAll(
        bool? unmapped,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return SearchInternal(new FacilityLocationLocalCodeMappingSearchModel
        {
            Unmapped = unmapped,
            PageSize = pageSize,
            PageNumber = pageNumber
        }, cancellationToken);
    }

    [HttpGet("{mappingId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityLocationLocalCodeMappingModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FacilityLocationLocalCodeMappingModel>> Get(string mappingId, CancellationToken cancellationToken = default)
    {
        mappingId = mappingId.Sanitize();
        if (string.IsNullOrWhiteSpace(mappingId))
        {
            return Problem(detail: "A mapping identifier must be provided.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var mapping = await _mappingQueries.Get(mappingId, cancellationToken);
            return mapping == null
                ? Problem(detail: "The requested mapping does not exist.", statusCode: StatusCodes.Status404NotFound)
                : Ok(mapping);
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

    [HttpGet("facilities/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityLocationLocalCodeMappingModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> GetForFacility(
        string facilityId,
        bool? unmapped,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return SearchForFacilityInternal(facilityId, new FacilityLocationLocalCodeMappingSearchModel
        {
            Unmapped = unmapped,
            PageSize = pageSize,
            PageNumber = pageNumber
        }, cancellationToken);
    }

    [HttpGet("facilities/{facilityId}/locations/{locationId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityLocationLocalCodeMappingModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> GetForLocation(
        string facilityId,
        string locationId,
        bool? unmapped,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return SearchForFacilityInternal(facilityId, new FacilityLocationLocalCodeMappingSearchModel
        {
            LocationId = locationId,
            Unmapped = unmapped,
            PageSize = pageSize,
            PageNumber = pageNumber
        }, cancellationToken);
    }

    [HttpGet("facilities/{facilityId}/local-codes/{localCode}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityLocationLocalCodeMappingModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> GetForLocalCode(
        string facilityId,
        string localCode,
        bool? unmapped,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return SearchForFacilityInternal(facilityId, new FacilityLocationLocalCodeMappingSearchModel
        {
            LocalCode = localCode,
            Unmapped = unmapped,
            PageSize = pageSize,
            PageNumber = pageNumber
        }, cancellationToken);
    }

    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<FacilityLocationLocalCodeMappingModel>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> Search(
        [FromQuery] FacilityLocationLocalCodeMappingSearchModel model,
        CancellationToken cancellationToken = default)
    {
        return SearchInternal(model, cancellationToken);
    }

    [HttpPost("facilities/{facilityId}")]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityLocationLocalCodeMappingModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FacilityLocationLocalCodeMappingModel>> Post(
        string facilityId,
        FacilityLocationLocalCodeMappingPostModel model,
        CancellationToken cancellationToken = default)
    {
        facilityId = facilityId.Sanitize();
        var sanitizedModel = Sanitize(model);
        var validationError = Validate(facilityId, sanitizedModel.LocationId, sanitizedModel.LocalCodeSystem, sanitizedModel.LocalCode, sanitizedModel.HSLOCId);
        if (validationError != null)
        {
            return Problem(detail: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var mapping = await _mappingManager.Create(facilityId, sanitizedModel, cancellationToken);
            return CreatedAtAction(nameof(Get), new { mappingId = mapping.Id }, mapping);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("{mappingId}")]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(FacilityLocationLocalCodeMappingModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FacilityLocationLocalCodeMappingModel>> Put(
        string mappingId,
        FacilityLocationLocalCodeMappingPutModel model,
        CancellationToken cancellationToken = default)
    {
        mappingId = mappingId.Sanitize();
        var sanitizedModel = Sanitize(model);
        var validationError = Validate(mappingId, sanitizedModel.LocalCodeSystem, sanitizedModel.LocalCode, sanitizedModel.HSLOCId);
        if (validationError != null)
        {
            return Problem(detail: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var mapping = await _mappingManager.Update(mappingId, sanitizedModel, cancellationToken);
            return mapping == null
                ? Problem(detail: "The requested mapping does not exist.", statusCode: StatusCodes.Status404NotFound)
                : AcceptedAtAction(nameof(Get), new { mappingId }, mapping);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{mappingId}")]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(string mappingId, CancellationToken cancellationToken = default)
    {
        mappingId = mappingId.Sanitize();
        if (string.IsNullOrWhiteSpace(mappingId))
        {
            return Problem(detail: "A mapping identifier must be provided.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await _mappingManager.Delete(mappingId, cancellationToken);
            return NoContent();
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

    [HttpDelete("facilities/{facilityId}")]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        facilityId = facilityId.Sanitize();
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return Problem(detail: "A facility identifier must be provided.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await _mappingManager.DeleteForFacility(facilityId, cancellationToken);
            return NoContent();
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

    private async Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> SearchForFacilityInternal(
        string facilityId,
        FacilityLocationLocalCodeMappingSearchModel model,
        CancellationToken cancellationToken)
    {
        model.FacilityId = facilityId;
        model = Sanitize(model);

        if (string.IsNullOrWhiteSpace(model.FacilityId))
        {
            return Problem(detail: "A facility identifier must be provided.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (model.LocationId != null && string.IsNullOrWhiteSpace(model.LocationId))
        {
            return Problem(detail: "A location identifier must be provided.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (model.LocalCode != null && string.IsNullOrWhiteSpace(model.LocalCode))
        {
            return Problem(detail: "A local code must be provided.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await SearchInternal(model, cancellationToken);
    }

    private async Task<ActionResult<PagedConfigModel<FacilityLocationLocalCodeMappingModel>>> SearchInternal(
        FacilityLocationLocalCodeMappingSearchModel model,
        CancellationToken cancellationToken)
    {
        model = Sanitize(model);
        try
        {
            return Ok(await _mappingQueries.Search(model, cancellationToken));
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

    private static FacilityLocationLocalCodeMappingPostModel Sanitize(FacilityLocationLocalCodeMappingPostModel model) => new()
    {
        LocationId = model.LocationId.Sanitize(),
        LocalCodeSystem = model.LocalCodeSystem.Sanitize(),
        LocalCode = model.LocalCode.Sanitize(),
        HSLOCId = model.HSLOCId
    };

    private static FacilityLocationLocalCodeMappingPutModel Sanitize(FacilityLocationLocalCodeMappingPutModel model) => new()
    {
        LocalCodeSystem = model.LocalCodeSystem.Sanitize(),
        LocalCode = model.LocalCode.Sanitize(),
        HSLOCId = model.HSLOCId
    };

    private static FacilityLocationLocalCodeMappingSearchModel Sanitize(FacilityLocationLocalCodeMappingSearchModel model) => new()
    {
        Id = model.Id?.Sanitize(),
        FacilityId = model.FacilityId?.Sanitize(),
        LocationId = model.LocationId?.Sanitize(),
        LocalCodeSystem = model.LocalCodeSystem?.Sanitize(),
        LocalCode = model.LocalCode?.Sanitize(),
        HSLOCId = model.HSLOCId,
        Unmapped = model.Unmapped,
        PageSize = model.PageSize,
        PageNumber = model.PageNumber
    };

    private static string? Validate(
        string identifier,
        string firstRequiredField,
        string secondRequiredField,
        Guid? hslocId) =>
        string.IsNullOrWhiteSpace(identifier)
            ? "An identifier must be provided."
            : string.IsNullOrWhiteSpace(firstRequiredField)
                ? "A required mapping field must be provided."
                : string.IsNullOrWhiteSpace(secondRequiredField)
                    ? "A required mapping field must be provided."
                    : hslocId == Guid.Empty
                        ? "HSLOCId must be a valid identifier when supplied."
                        : null;

    private static string? Validate(
        string facilityId,
        string locationId,
        string localCodeSystem,
        string localCode,
        Guid? hslocId)
    {
        var validationResult = Validate(facilityId, locationId, localCodeSystem, hslocId);
        return validationResult ?? Validate(facilityId, localCode, "mapping", hslocId);
    }
}