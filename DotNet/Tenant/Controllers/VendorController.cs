using AutoMapper;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace LantanaGroup.Link.Tenant.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class VendorController : ControllerBase
    {
        private readonly IVendorManager _vendorManager;
        private readonly IVendorQueries _vendorQueries;
        private readonly ILogger<VendorController> _logger;

        public VendorController(ILogger<VendorController> logger,
            IVendorManager vendorManager, IVendorQueries vendorQueries)
        {
            _vendorManager = vendorManager;
            _vendorQueries = vendorQueries;
            _logger = logger;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VendorModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorModel>> Get(string id)
        {
            try
            {
                if(Guid.TryParse(id, out Guid parsedId) == false)
                {
                    return NotFound();
                }

                var vendor = await _vendorQueries.GetVendor(parsedId);
                if(vendor == null)
                {
                    return NotFound();
                }
                return Ok(vendor);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<VendorModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<VendorModel>>> GetAll()
        {
            try
            {
                return Ok(await _vendorQueries.GetAll());
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(VendorModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorModel>> Post(CreateVendorModel createVendorModel)
        {
            try
            {
                var newVendor = await _vendorManager.CreateVendorAsync(new VendorModel { Name = createVendorModel.Name });
                return CreatedAtAction(nameof(Get), new { id = newVendor.Id }, newVendor);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VendorModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorModel>> Put(string id, UpdateVendorModel updateVendorModel)
        {
            try
            {
                if(Guid.TryParse(id, out Guid parsedId) == false)
                {
                    return BadRequest();
                }
                var existingVendor = await _vendorQueries.GetVendor(parsedId);
                if (existingVendor == null)
                {
                    return BadRequest();
                }
                
                var vendorModel = new VendorModel
                {
                    Id = existingVendor.Id,
                    Name = updateVendorModel.Name
                };
                var updatedVendor = await _vendorManager.UpdateVendorAsync(parsedId, vendorModel);
                return Ok(updatedVendor);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest();
                }

                if (Guid.TryParse(id, out var parsedId) == false)
                {
                    return BadRequest();
                }
                
                if(await _vendorQueries.GetVendor(parsedId) == null)
                {
                    return BadRequest();
                }
                await _vendorManager.DeleteVendorAsync(parsedId);

                return NoContent();
            }
            catch (VendorVersionInUseException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}