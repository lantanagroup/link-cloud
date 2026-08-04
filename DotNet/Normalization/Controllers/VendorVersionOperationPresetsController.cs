using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Filters;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers;

[Route("api/normalization/vendor-version-operation-presets")]
[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
public class VendorVersionOperationPresetsController : ControllerBase
{
    private readonly IVendorVersionOperationPresetManager _presetManager;
    private readonly IVendorVersionOperationPresetQueries _presetQueries;

    public VendorVersionOperationPresetsController(
        IVendorVersionOperationPresetManager presetManager,
        IVendorVersionOperationPresetQueries presetQueries)
    {
        _presetManager = presetManager;
        _presetQueries = presetQueries;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<VendorVersionOperationPresetModel>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<VendorVersionOperationPresetModel>>> GetAll(Guid? vendorVersionId = null, string? resource = null)
    {
        try
        {
            return Ok(await _presetQueries.Search(new VendorVersionOperationPresetSearchModel
            {
                VendorVersionId = vendorVersionId,
                Resource = resource
            }));
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("{presetId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VendorVersionOperationPresetModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VendorVersionOperationPresetModel>> Get(Guid presetId)
    {
        try
        {
            var preset = await _presetQueries.Get(presetId);
            return preset == null ? NotFound() : Ok(preset);
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryOrBearerToken]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(VendorVersionOperationPresetModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VendorVersionOperationPresetModel>> Post(VendorVersionOperationPresetPostModel model)
    {
        if (model.VendorVersionId == null || model.VendorVersionId == Guid.Empty)
        {
            return BadRequest("VendorVersionId is required.");
        }

        if (model.OperationResourceTypeId == null || model.OperationResourceTypeId == Guid.Empty)
        {
            return BadRequest("OperationResourceTypeId is required.");
        }

        try
        {
            var preset = await _presetManager.Create(new CreateVendorVersionOperationPresetModel
            {
                VendorVersionId = model.VendorVersionId.Value,
                OperationResourceTypeId = model.OperationResourceTypeId.Value
            });

            return CreatedAtAction(nameof(Get), new { presetId = preset.Id }, preset);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{vendorVersionId:guid}/{presetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid vendorVersionId, Guid presetId)
    {
        try
        {
            await _presetManager.Delete(vendorVersionId, presetId);
            return NoContent();
        }
        catch (Exception exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}