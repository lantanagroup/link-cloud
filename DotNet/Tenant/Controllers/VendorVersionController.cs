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
using LantanaGroup.Link.Tenant.Business.Models;
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
    public class VendorVersionController : ControllerBase
    {
        private readonly IVendorManager _vendorManager;
        private readonly IVendorQueries _vendorQueries;
        private readonly ILogger<VendorVersionController> _logger;

        public VendorVersionController(ILogger<VendorVersionController> logger,
            IVendorManager vendorManager, IVendorQueries vendorQueries)
        {
            _vendorManager = vendorManager;
            _vendorQueries = vendorQueries;
            _logger = logger;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VendorVersionModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorVersionModel>> Get(string id)
        {
            try
            {
                if(Guid.TryParse(id, out Guid parsedId) == false)
                {
                    return NotFound();
                }

                var vendorVersion = await _vendorQueries.GetVendorVersion(parsedId);
                if(vendorVersion == null)
                {
                    return NotFound();
                }
                return Ok(vendorVersion);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<VendorVersionModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<VendorVersionModel>>> GetAll(string? vendorId = null)
        {
            try
            {
                if(string.IsNullOrEmpty(vendorId))
                {
                    return Ok(await _vendorQueries.GetAllVendorVersions());
                }
                else
                {
                    if(Guid.TryParse(vendorId, out Guid parsedVendorId) == false)
                    {
                        return BadRequest();
                    }
                    return Ok(await _vendorQueries.GetVendorVersionsByVendorId(parsedVendorId));
                }
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(VendorVersionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorVersionModel>> Post(CreateVendorVersionModel createVendorVersionModel)
        {
            try
            {
                var existingVendor = await _vendorQueries.GetVendor(createVendorVersionModel.VendorId!.Value);
                if(existingVendor == null)
                {
                    return BadRequest();
                }
                var newVendorVersion = await _vendorManager.CreateVendorVersionAsync(new VendorVersionModel { VendorId = createVendorVersionModel.VendorId, Version = createVendorVersionModel.Version });
                return CreatedAtAction(nameof(Get), new { id = newVendorVersion.Id }, newVendorVersion);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VendorVersionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VendorVersionModel>> Put(string id, UpdateVendorVersionModel updateVendorVersionModel)
        {
            try
            {
                if(Guid.TryParse(id, out Guid parsedId) == false)
                {
                    return BadRequest();
                }
                var existingVendorVersion = await _vendorQueries.GetVendorVersion(parsedId);
                if (existingVendorVersion == null)
                {
                    return BadRequest();
                }
                
                var vendorVersionModel = new VendorVersionModel
                {
                    Id = existingVendorVersion.Id,
                    VendorId = existingVendorVersion.VendorId,
                    Version = updateVendorVersionModel.Version
                };
                var updatedVendorVersion = await _vendorManager.UpdateVendorVersionAsync(parsedId, vendorVersionModel);
                return Ok(updatedVendorVersion);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
                
                if(await _vendorQueries.GetVendorVersion(parsedId) == null)
                {
                    return BadRequest();
                }
                await _vendorManager.DeleteVendorVersionAsync(parsedId);

                return NoContent();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}