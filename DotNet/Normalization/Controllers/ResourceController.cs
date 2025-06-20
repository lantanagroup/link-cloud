using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/normalization/[controller]")]
    [ApiController]
    public class ResourceController : ControllerBase
    {
        private readonly IResourceManager _resourceManager;
        private readonly IResourceQueries _resourceQueries; 
        public ResourceController(IResourceManager resourceManager, IResourceQueries resourceQueries) 
        {
            _resourceManager = resourceManager;
            _resourceQueries = resourceQueries;
        }

        [HttpGet("{resource}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResourceModel))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResourceModel>> Get(string resource)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var foundResource = await _resourceQueries.Get(resource);

                if (foundResource == null)
                {
                    return NoContent();
                }

                return Ok(foundResource);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("resources")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ResourceModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ResourceModel>>> Get()
        {
            try
            {
                var foundResources = await _resourceQueries.GetAll();

                return Ok(foundResources);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("initialize")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ResourceModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ResourceModel>>> Initialize()
        {
            try
            {
                var resourceModels = await _resourceManager.InitializeResources();

                return Ok(resourceModels);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{resource}")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ResourceModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResourceModel>> Post(string resource)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var createdResource = await _resourceManager.CreateResource(resource);

                if (createdResource == null)
                {
                    return Conflict($"Resource '{resource}' already exists.");
                }

                return Created("", createdResource);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{resource}/bypass")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ResourceModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policy = PolicyNames.IsLinkAdmin)]
        public async Task<ActionResult<ResourceModel>> PostWithBypass(string resource)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var foundResource = await _resourceManager.CreateResource(resource, true);

                if (foundResource == null)
                {
                    return null;
                }

                return Created("", foundResource);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{resource}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policy = PolicyNames.IsLinkAdmin)]
        public async Task<IActionResult> Delete(string resource)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                await _resourceManager.DeleteResource(resource);

                return Accepted();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
