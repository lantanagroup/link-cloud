using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Filters;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/encounter-mappings")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class EncounterMappingController : Controller
{
    // Sort is supported on the scalar EncounterMapping columns; LocationId is not sortable because it
    // is a derived, multi-valued projection (resolved via join after paging), not a column on the row.
    private static readonly HashSet<string> AllowedSortBy = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(EncounterMappingModel.EncounterMappingId),
        nameof(EncounterMappingModel.EncounterId),
        nameof(EncounterMappingModel.PatientId),
        nameof(EncounterMappingModel.MappedToOrg),
        nameof(EncounterMappingModel.CreateDate),
        nameof(EncounterMappingModel.ModifiedDate)
    };

    private readonly ILogger<EncounterMappingController> _logger;
    private readonly IEncounterMappingManager _manager;
    private readonly IEncounterMappingQueries _queries;

    public EncounterMappingController(
        ILogger<EncounterMappingController> logger,
        IEncounterMappingManager manager,
        IEncounterMappingQueries queries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    /// <summary>
    /// GET /api/data/encounter-mappings/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [ActionName(nameof(GetByIdAsync))]
    [ProducesResponseType(typeof(EncounterMappingModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetByIdAsync(int id)
    {
        try
        {
            var result = await _queries.GetByIdAsync(id);

            if (result == null)
                return Problem(title: "Not Found",
                               detail: $"EncounterMapping with id {id} not found.",
                               statusCode: (int)HttpStatusCode.NotFound);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving EncounterMapping with id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /api/data/encounter-mappings/facilities/{facilityId}
    /// Returns 200 OK with empty list [] when none exist.
    /// </summary>
    [HttpGet("facilities/{facilityId}")]
    [ProducesResponseType(typeof(List<EncounterMappingModel>), StatusCodes.Status200OK)]
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
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving EncounterMappings for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /api/data/encounter-mappings/facilities/{facilityId}/encounters/{encounterId}
    /// </summary>
    [HttpGet("facilities/{facilityId}/encounters/{encounterId}")]
    [ProducesResponseType(typeof(EncounterMappingModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            if (string.IsNullOrWhiteSpace(encounterId))
                throw new BadRequestException("encounterId is required.");

            facilityId = facilityId.SanitizeAndRemove();
            encounterId = encounterId.SanitizeAndRemove();

            var result = await _queries.GetByFacilityIdAndEncounterIdAsync(facilityId, encounterId);

            if (result == null)
                return Problem(title: "Not Found",
                               detail: $"EncounterMapping for facilityId {facilityId.Sanitize()} and encounterId {encounterId.Sanitize()} not found.",
                               statusCode: (int)HttpStatusCode.NotFound);

            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving EncounterMapping for facilityId {facilityId} and encounterId {encounterId}",
                facilityId.Sanitize(), encounterId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /api/data/encounter-mappings/facilities/{facilityId}/patients/{patientId}
    /// Returns 200 OK with empty list [] when none exist.
    /// </summary>
    [HttpGet("facilities/{facilityId}/patients/{patientId}")]
    [ProducesResponseType(typeof(List<EncounterMappingModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetByFacilityIdAndPatientIdAsync(string facilityId, string patientId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            if (string.IsNullOrWhiteSpace(patientId))
                throw new BadRequestException("patientId is required.");

            facilityId = facilityId.SanitizeAndRemove();
            patientId = patientId.SanitizeAndRemove();

            var result = await _queries.GetByFacilityIdAndPatientIdAsync(facilityId, patientId);
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving EncounterMappings for facilityId {facilityId} and patientId {patientId}",
                facilityId.Sanitize(), patientId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /api/data/encounter-mappings/facilities/{facilityId}/search
    /// </summary>
    [HttpGet("facilities/{facilityId}/search")]
    [ProducesResponseType(typeof(PagedConfigModel<EncounterMappingModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SearchAsync(
        string facilityId,
        [FromQuery] EncounterMappingSearchParameters searchParams,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        SortOrder? sortOrder = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            if (!string.IsNullOrWhiteSpace(sortBy) && !AllowedSortBy.Contains(sortBy))
                throw new BadRequestException($"Invalid sortBy. Allowed values: {string.Join(", ", AllowedSortBy)}");

            searchParams ??= new EncounterMappingSearchParameters();

            var search = new EncounterMappingSearchModel
            {
                FacilityId = facilityId.SanitizeAndRemove(),
                PatientId = searchParams.PatientId,
                EncounterId = searchParams.EncounterId,
                MappedToOrg = searchParams.MappedToOrg
            };

            var result = await _queries.SearchAsync(search, pageNumber, pageSize, sortBy, sortOrder);

            // Always 200, even when empty, so the UI can render its empty state with valid paging metadata.
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while searching EncounterMappings for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// PUT /api/data/encounter-mappings/{id}
    /// Updates the MappedToOrg flag for the specified EncounterMapping.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EncounterMappingModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateByIdAsync(int id, [FromBody] UpdateEncounterMappingApiModel model)
    {
        try
        {
            if (model == null)
                throw new BadRequestException("Request body is required.");

            var updateModel = new UpdateEncounterMappingModel { MappedToOrg = model.MappedToOrg };
            var updated = await _manager.UpdateByIdAsync(id, updateModel);
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
            _logger.LogError(ex, "An exception occurred while updating EncounterMapping id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// PUT /api/data/encounter-mappings/facilities/{facilityId}/encounters/{encounterId}
    /// Updates the MappedToOrg flag for the specified encounter within a facility.
    /// </summary>
    [HttpPut("facilities/{facilityId}/encounters/{encounterId}")]
    [ProducesResponseType(typeof(EncounterMappingModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId, [FromBody] UpdateEncounterMappingApiModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            if (string.IsNullOrWhiteSpace(encounterId))
                throw new BadRequestException("encounterId is required.");

            if (model == null)
                throw new BadRequestException("Request body is required.");

            facilityId = facilityId.SanitizeAndRemove();
            encounterId = encounterId.SanitizeAndRemove();

            var updateModel = new UpdateEncounterMappingModel { MappedToOrg = model.MappedToOrg };
            var updated = await _manager.UpdateByFacilityIdAndEncounterIdAsync(facilityId, encounterId, updateModel);
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
            _logger.LogError(ex, "An exception occurred while updating EncounterMapping for facilityId {facilityId} and encounterId {encounterId}",
                facilityId.Sanitize(), encounterId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// POST /api/data/encounter-mappings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(typeof(EncounterMappingModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreateAsync([FromBody] CreateEncounterMappingModel model)
    {
        try
        {
            if (model == null)
            {
                throw new BadRequestException("Request body is required.");
            }

            model.FacilityId = model.FacilityId.SanitizeAndRemove();
            model.PatientId = model.PatientId.SanitizeAndRemove();
            model.EncounterId = model.EncounterId.SanitizeAndRemove();

            // re-evaluate after sanitization
            ModelState.Clear(); 
            if (!TryValidateModel(model))
            {
                return ValidationProblem(ModelState);
            }

            var created = await _manager.CreateAsync(model);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = created.EncounterMappingId }, created);
        }
        catch (EntityAlreadyExistsException ex)
        {
            _logger.LogError(ex, "EntityAlreadyExistsException occurred.");
            return Problem(title: "Conflict",
                detail: "The request could not be completed because it conflicts with the current state of the resource.",
                statusCode: (int)HttpStatusCode.Conflict);
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "NotFoundException occurred.");
            return Problem(title: "Not Found", detail: ex.Message, statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while creating an EncounterMapping.");
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}
