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
    [Route("api/dmrp/facility-reporting-plans")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class FacilityReportingPlansController : ControllerBase
    {
        private readonly ILogger<FacilityReportingPlansController> _logger;
        private readonly IFacilityReportingPlanManager _manager;
        private readonly IFacilityReportingPlanQueries _queries;

        public FacilityReportingPlansController(ILogger<FacilityReportingPlansController> logger, IFacilityReportingPlanManager manager, IFacilityReportingPlanQueries queries)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Get a paged list of facility reporting plans.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedFacilityReportingPlanDto))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilityReportingPlans")]
        public async Task<IActionResult> GetFacilityReportingPlans(string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            sortBy = sortBy?.Sanitize();

            if (pageSize < 1 || pageSize > 100)
            {
                pageSize = 10;
            }

            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Reporting Plans");

            var result = await _queries.PagedSearchAsync(sortBy ?? "Id", sortOrder ?? SortOrder.Descending,
                pageSize, pageNumber, cancellationToken);

            if (result.Records.Count == 0)
            {
                return NoContent();
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets a facility reporting plan by Id.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityReportingPlanModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFacilityReportingPlan(string id, CancellationToken cancellationToken)
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
        /// Creates a facility reporting plan.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityReportingPlanModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> CreateFacilityReportingPlan(FacilityReportingPlanModel request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest();
            }

            FacilityReportingPlan created;

            try
            {
                var entity = new FacilityReportingPlan();
                // TODO: Map `request` to `entity`
                created = await _manager.CreateAsync(entity, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityReportingPlansController.CreateFacilityReportingPlan");
                return Problem("An error occurred while creating the facility reporting plan", null, 500);
            }

            var model = new FacilityReportingPlanModel { Id = created.Id };
            // TODO: Map `created` to `model`

            return Created($"/api/dmrp/facility-reporting-plans/{model.Id}", model);
        }

        /// <summary>
        /// Updates a facility reporting plan.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(FacilityReportingPlanModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFacilityReportingPlan(string id, FacilityReportingPlanModel request, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            if (request?.Id != null && request.Id != id)
            {
                return BadRequest("Id in the URL must match the Id in the request body.");
            }

            try
            {
                await _manager.UpdateAsync(id, new FacilityReportingPlan { Id = id }, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityReportingPlansController.UpdateFacilityReportingPlan");
                return Problem("An error occurred while updating the facility reporting plan", null, 500);
            }

            var model = await _queries.GetAsync(id, cancellationToken);

            return Accepted(model);
        }

        /// <summary>
        /// Deletes a facility reporting plan.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFacilityReportingPlan(string id, CancellationToken cancellationToken)
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
                _logger.LogError(ex, "Exception encountered in FacilityReportingPlansController.DeleteFacilityReportingPlan");
                return Problem("An error occurred while deleting the facility reporting plan", null, 500);
            }

            return NoContent();
        }
    }
}
