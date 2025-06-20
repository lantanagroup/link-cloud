using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorModel>> Get(string vendor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendor))
                {
                    return base.BadRequest("Required parameter 'vendor' cannot be null, empty, or whitespace.");
                }

                var foundVendor = await _vendorQueries.GetVendor(vendor);

                if (foundVendor == null)
                {
                    return NoContent();
                }

                return Ok(foundVendor);
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
        public async Task<ActionResult<ResourceModel>> Post(string vendor)
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
                    return Conflict($"Resource '{createdVendor}' already exists.");
                }

                var createdVendorVersion = await _vendorManager.CreateVendorVersion(new CreateVendorVersionModel()
                {
                    VendorId = createdVendor.Id,
                    Version = "default"
                });

                return Created("", _vendorQueries.GetVendor(createdVendor.Id));
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{vendor}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
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

                await _vendorManager.DeleteVendor(vendor);

                return Accepted();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
