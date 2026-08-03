using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.DMRP.Controllers
{
    [Route("api/dmrp/measure-mappings")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class MeasureMappingsController : ControllerBase
    {
        private readonly ILogger<MeasureMappingsController> _logger;
        private readonly IMeasureMappingManager _manager;
        private readonly IMeasureMappingQueries _queries;

        public MeasureMappingsController(ILogger<MeasureMappingsController> logger, IMeasureMappingManager manager, IMeasureMappingQueries queries)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Get a paged list of measure mappings.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedMeasureMappingDto))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetMeasureMappings")]
        public async Task<IActionResult> GetMeasureMappings(string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            sortBy = sortBy?.Sanitize();

            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Measure Mappings");

            var result = await _queries.PagedSearchAsync(sortBy ?? "Id", sortOrder ?? SortOrder.Descending,
                pageSize, pageNumber, cancellationToken);

            if (result.Records.Count == 0)
            {
                return NoContent();
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets a measure mapping by Id.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MeasureMappingModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMeasureMapping(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            var model = await _queries.GetAsync(id, cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return Ok(model);
        }

        /// <summary>
        /// Creates a measure mapping.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(MeasureMappingModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> CreateMeasureMapping(MeasureMappingModel request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest();
            }

            MeasureMapping created;

            try
            {
                created = await _manager.CreateAsync(new MeasureMapping(), cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.CreateMeasureMapping");
                return Problem("An error occurred while creating the measure mapping", null, 500);
            }

            var model = new MeasureMappingModel { Id = created.Id };

            return Created($"/api/dmrp/measure-mappings/{model.Id}", model);
        }

        /// <summary>
        /// Updates a measure mapping.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(MeasureMappingModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMeasureMapping(string id, MeasureMappingModel request, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            if (request?.Id != null && request.Id != id)
            {
                return BadRequest("Id in the URL must match the Id in the request body.");
            }

            try
            {
                await _manager.UpdateAsync(id, new MeasureMapping { Id = id }, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.UpdateMeasureMapping");
                return Problem("An error occurred while updating the measure mapping", null, 500);
            }

            var model = await _queries.GetAsync(id, cancellationToken);

            return Accepted(model);
        }

        /// <summary>
        /// Deletes a measure mapping.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeasureMapping(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            try
            {
                await _manager.DeleteAsync(id, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.DeleteMeasureMapping");
                return Problem("An error occurred while deleting the measure mapping", null, 500);
            }

            return NoContent();
        }
    }
}
