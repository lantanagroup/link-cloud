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


[Route("api/data/location-config")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class OrganizationLocationConfigurationController : Controller
{
    private readonly ILogger<OrganizationLocationConfigurationController> _logger;
    private readonly IOrganizationLocationConfigurationManager _manager;
    private readonly IOrganizationLocationConfigurationQueries _queries;

    public OrganizationLocationConfigurationController(
        ILogger<OrganizationLocationConfigurationController> logger,
        IOrganizationLocationConfigurationManager manager,
        IOrganizationLocationConfigurationQueries queries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    /// <summary>
    /// GET /location-org-configs/{id}
    /// </summary>
    [HttpGet("{id:int}", Name = nameof(GetByIdAsync))]
    [ProducesResponseType(typeof(OrganizationLocationConfigurationModel), StatusCodes.Status200OK)]
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
            _logger.LogWarning(ex, "OrganizationLocationConfiguration with id {id} not found.", id);
            return Problem(title: "Not Found",
                           detail: $"OrganizationLocationConfiguration with id {id} not found.",
                           statusCode: (int)HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while retrieving OrganizationLocationConfiguration with id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /location-org-configs/facility/{facilityId}
    /// </summary>
    [HttpGet("facility/{facilityId}")]
    [ProducesResponseType(typeof(List<OrganizationLocationConfigurationModel>), StatusCodes.Status200OK)]
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
            _logger.LogError(ex, "An exception occurred while retrieving OrganizationLocationConfiguration for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// GET /location-org-configs/facility/{facilityId}/search
    /// </summary>
    [HttpGet("facility/{facilityId}/search")]
    [ProducesResponseType(typeof(PagedConfigModel<OrganizationLocationConfigurationModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SearchAsync(
        string facilityId,
        [FromQuery] OrganizationLocationConfigurationSearchParameters searchParams,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            // Construct the internal search model (FacilityId comes from route only)
            var search = new OrganizationLocationConfigurationSearchModel
            {
                FacilityId = facilityId.SanitizeAndRemove(),
                ConfigId = searchParams.ConfigId,
                IsActive = searchParams.IsActive,
                DescriptionContains = searchParams.DescriptionContains
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
            _logger.LogError(ex, "An exception occurred while searching OrganizationLocationConfiguration for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// POST /location-org-configs/facility/{facilityId}
    /// </summary>
    [HttpPost("facility/{facilityId}")]
    [ProducesResponseType(typeof(OrganizationLocationConfigurationModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAsync(
        string facilityId,
        [FromBody] CreateOrganizationLocationConfigurationApiModel model)
    {
        try
        {
            if (model == null)
                throw new BadRequestException("request body is required.");

            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            facilityId = facilityId.SanitizeAndRemove();

            var createModel = new CreateOrganizationLocationConfigurationModel
            {
                FacilityId = facilityId,
                Description = model.Description,
                IsActive = model.IsActive,
                Conditions = model.Conditions
            };

            var created = await _manager.CreateAsync(createModel);

            return CreatedAtRoute(nameof(GetByIdAsync), new { id = created.ConfigId }, created);
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException occurred.");
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while creating OrganizationLocationConfiguration for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// PUT /location-org-configs/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OrganizationLocationConfigurationModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateByIdAsync(int id, [FromBody] UpdateOrganizationLocationConfigurationModel model)
    {
        try
        {
            if (model == null)
                throw new BadRequestException("request body is required.");

            var updated = await _manager.UpdateByIdAsync(id, model);
            return Accepted(updated);
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
            _logger.LogError(ex, "An exception occurred while updating OrganizationLocationConfiguration id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// PUT /location-org-configs/facility/{facilityId} (updates ALL configs for the facility)
    /// </summary>
    [HttpPut("facility/{facilityId}")]
    [ProducesResponseType(typeof(List<OrganizationLocationConfigurationModel>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateByFacilityIdAsync(
        string facilityId,
        [FromBody] UpdateOrganizationLocationConfigurationModel model)
    {
        try
        {
            if (model == null)
                throw new BadRequestException("request body is required.");

            if (string.IsNullOrWhiteSpace(facilityId))
                throw new BadRequestException("facilityId is required.");

            facilityId = facilityId.SanitizeAndRemove();

            var updatedList = await _manager.UpdateByFacilityIdAsync(facilityId, model);
            return Accepted(updatedList);
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
            _logger.LogError(ex, "An exception occurred while bulk updating OrganizationLocationConfiguration for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// DELETE /location-org-configs/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteByIdAsync(int id)
    {
        try
        {
            await _manager.DeleteByIdAsync(id);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while deleting OrganizationLocationConfiguration id {id}", id);
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// DELETE /location-org-configs/facility/{facilityId}
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
            _logger.LogError(ex, "An exception occurred while deleting OrganizationLocationConfiguration for facilityId {facilityId}", facilityId.Sanitize());
            return Problem(title: "Internal Server Error", detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}