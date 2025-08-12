using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/normalization/[controller]")]
    [ApiController]
    public class VendorController : ControllerBase
    {
        private readonly IVendorManager _vendorManager;
        private readonly IVendorQueries _vendorQueries; 
        public VendorController(IVendorManager vendorManager, IVendorQueries vendorQueries) 
        {
            _vendorManager = vendorManager;
            _vendorQueries = vendorQueries;
        }

        [HttpGet("{vendor}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VendorModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorModel>> Get(string vendor)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(vendor))
                {
                    return BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                var foundVendor = await _vendorQueries.SearchVendors(new VendorSearchModel()
                {
                    VendorName = vendor
                });

                return Ok(foundVendor);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("vendors")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<VendorModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VendorModel>>> GetAll()
        {
            try
            {
                var foundVendors = await _vendorQueries.GetAllVendors();

                return Ok(foundVendors);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{vendor}")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(VendorModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorModel>> Post(string vendor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor))
                {
                    return BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                var createdVendor = await _vendorManager.CreateVendor(vendor);

                if (createdVendor == null)
                {
                    return Conflict($"Vendor '{vendor}' already exists.");
                }

                var createdVendorVersion = await _vendorManager.CreateVendorVersion(new CreateVendorVersionModel()
                {
                    VendorId = createdVendor.Id,
                    Version = "default"
                });

                return Created("", await _vendorQueries.GetVendor(createdVendor.Id));
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{vendor}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policy = PolicyNames.IsLinkAdmin)]
        public async Task<IActionResult> Delete(string vendor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor))
                {
                    return BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                if (Guid.TryParse(vendor, out var vendorId))
                {
                    await _vendorManager.DeleteVendor(vendorId);
                }
                else
                {
                    await _vendorManager.DeleteVendor(vendor);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("presets/{vendor}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<VendorVersionOperationPresetModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VendorVersionOperationPresetModel>>> GetVendorOperationPresets(string vendor, string? resource = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor))
                {
                    return BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                VendorModel? foundVendor;
                if (Guid.TryParse(vendor, out var vendorId))
                {
                    foundVendor = await _vendorQueries.GetVendor(vendorId);
                }
                else
                {
                    foundVendor = await _vendorQueries.GetVendor(vendor);

                    if(foundVendor == null)
                    {
                        return base.BadRequest($"No vendor by the name {vendor.Sanitize()} found.");
                    }
                }

                var vendorPresets = await _vendorQueries.SearchVendorVersionOperationPreset(new VendorOperationPresetSearchModel()
                {
                    VendorId = foundVendor.Id,
                    Resource = string.IsNullOrWhiteSpace(resource) ? null : resource
                });

                return Ok(vendorPresets);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("presets/{vendor}/{presetId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<VendorVersionOperationPresetModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VendorVersionOperationPresetModel>>> GetVendorOperationPresets(string vendor, Guid presetId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor))
                {
                    return base.BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                if(presetId == Guid.Empty)
                {
                    return base.BadRequest("Required parameter 'presetId' cannot be null or empty.");
                }

                VendorModel? foundVendor;
                if (Guid.TryParse(vendor, out var vendorId))
                {
                    foundVendor = await _vendorQueries.GetVendor(vendorId);
                }
                else
                {
                    foundVendor = await _vendorQueries.GetVendor(vendor);

                    if (foundVendor == null)
                    {
                        return base.BadRequest($"No vendor by the name {vendor.Sanitize()} found.");
                    }
                }

                var vendorPresets = await _vendorQueries.SearchVendorVersionOperationPreset(new VendorOperationPresetSearchModel()
                {
                    VendorId = foundVendor.Id,
                    Id = presetId
                });

                return Ok(vendorPresets);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("presets")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(VendorVersionOperationPresetModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorVersionOperationPresetModel>> Post(VendorVersionOperationPresetPostModel model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest("Required body 'model' cannot be null");
                }

                var vendorVersion = await _vendorQueries.GetVendorVersion(model.VendorId);

                var vendorPrest = await _vendorManager.CreateVendorVersionOperationPreset(new CreateVendorVersionOperationPresetModel()
                {
                    OperationResourceTypeId = model.OperationResourceTypeId,
                    VendorVersionId = vendorVersion.Id
                });

                return Created("", vendorPrest);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("presets/{vendor}/{presetId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policy = PolicyNames.IsLinkAdmin)]
        public async Task<IActionResult> Delete(string vendor, Guid presetId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor))
                {
                    return base.BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                if (presetId == Guid.Empty)
                {
                    return base.BadRequest("Required parameter 'presetId' cannot be null or empty.");
                }

                VendorModel? foundVendor;
                if (Guid.TryParse(vendor, out var vendorId))
                {
                    foundVendor = await _vendorQueries.GetVendor(vendorId);
                }
                else
                {
                    foundVendor = await _vendorQueries.GetVendor(vendor);

                    if (foundVendor == null)
                    {
                        return base.BadRequest($"No vendor by the name {vendor.Sanitize()} found.");
                    }
                }

                await _vendorManager.DeleteVendorVersionOperationPreset(foundVendor.Id, presetId);

                return NoContent();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
