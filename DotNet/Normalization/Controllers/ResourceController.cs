using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Models;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/normalization/[controller]")]
    [ApiController]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    public class ResourceController : ControllerBase
    {
        private readonly IResourceManager _resourceManager;
        private readonly IResourceQueries _resourceQueries;

        /// <summary>
        /// The request's audit context. Null when the controller is constructed directly, outside a request.
        /// </summary>
        private IAuditContext? Audit => HttpContext?.RequestServices?.GetService<IAuditContext>();

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
        public async Task<ActionResult<ResourceModel>> Get(string resource, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var foundResource = await _resourceQueries.Get(resource, cancellationToken);

                if (foundResource == null)
                {
                    return NoContent();
                }

                return Ok(foundResource);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
        public async Task<ActionResult<List<ResourceModel>>> Get(CancellationToken cancellationToken = default)
        {
            try
            {
                var foundResources = await _resourceQueries.GetAll(cancellationToken);

                return Ok(foundResources);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("initialize")]
        [Auditable(AuditEventType.Create, "Resource", "Bulk resource initialization")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ResourceModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<ActionResult<List<ResourceModel>>> Initialize(CancellationToken cancellationToken = default)
        {
            try
            {
                var resourceModels = await _resourceManager.InitializeResources(cancellationToken);

                return Ok(resourceModels);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{resource}")]
        [Auditable(AuditEventType.Create, "Resource")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ResourceModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<ActionResult<ResourceModel>> Post(string resource, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var createdResource = await _resourceManager.CreateResource(resource, cancellationToken: cancellationToken);

                if (createdResource == null)
                {
                    return Conflict($"Resource '{resource}' already exists.");
                }

                if (Audit is { } audit)
                {
                    audit.RecordId = createdResource.ResourceTypeId.ToString();
                }

                return Created("", createdResource);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{resource}/bypass")]
        [Auditable(AuditEventType.Create, "Resource")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ResourceModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<ActionResult<ResourceModel>> PostWithBypass(string resource, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var foundResource = await _resourceManager.CreateResource(resource, true, cancellationToken);

                if (foundResource == null)
                {
                    return null;
                }

                if (Audit is { } audit)
                {
                    audit.RecordId = foundResource.ResourceTypeId.ToString();
                }

                return Created("", foundResource);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{resource}")]
        [Auditable(AuditEventType.Delete, "Resource", "{resource} deleted")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<IActionResult> Delete(string resource, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resource))
                {
                    return BadRequest("Required parameter 'resource' cannot be null, empty, or whitespace.");
                }

                var deletedResourceTypeId = await _resourceManager.DeleteResource(resource, cancellationToken);

                if (Audit is { } audit)
                {
                    //the route carries the resource name, not the row's id.
                    audit.RecordId = deletedResourceTypeId.ToString();
                }


                return Accepted();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
