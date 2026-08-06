using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.DMRP.Models.Exceptions;
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
    [Route("api/dmrp/reporting-plans")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class FacilityReportingPlansController : ControllerBase
    {
        /// <summary>
        /// Columns a caller may sort by. The repository resolves sortBy reflectively and throws on an
        /// unknown property, so an unlisted value is refused here as a bad request rather than
        /// surfacing as a server error.
        /// </summary>
        private static readonly HashSet<string> SortableColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(FacilityReportingPlan.Id),
            nameof(FacilityReportingPlan.FacilityId),
            nameof(FacilityReportingPlan.MeasureMappingId),
            nameof(FacilityReportingPlan.ReportingMonth),
            nameof(FacilityReportingPlan.ReportingYear),
            nameof(FacilityReportingPlan.IsReporting),
            nameof(FacilityReportingPlan.CreateDate),
            nameof(FacilityReportingPlan.ModifyDate)
        };

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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilityReportingPlans")]
        public Task<IActionResult> GetFacilityReportingPlans(string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default) =>
            SearchFacilityReportingPlans(null, null, null, null, null, sortBy, sortOrder, pageSize, pageNumber,
                cancellationToken);

        /// <summary>
        /// Search facility reporting plans by any combination of facility, measure mapping, reporting
        /// period and reporting state.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedFacilityReportingPlanDto))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("search", Name = "SearchFacilityReportingPlans")]
        public async Task<IActionResult> SearchFacilityReportingPlans(string? facilityId, string? measureMappingId,
            int? month, int? year, bool? isReporting, string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            facilityId = facilityId?.Sanitize();
            measureMappingId = measureMappingId?.Sanitize();
            sortBy = sortBy?.Sanitize();

            var periodError = ValidatePeriodFilters(month, year);
            if (periodError is not null)
            {
                return BadRequest(periodError);
            }

            if (!string.IsNullOrWhiteSpace(sortBy) && !SortableColumns.Contains(sortBy))
            {
                return BadRequest($"Cannot sort by '{sortBy}'.");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                pageSize = 10;
            }

            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Search Facility Reporting Plans");

            var result = await _queries.PagedSearchAsync(NullIfBlank(facilityId), NullIfBlank(measureMappingId),
                month, year, isReporting, sortBy ?? nameof(FacilityReportingPlan.Id),
                sortOrder ?? SortOrder.Descending, pageSize, pageNumber, cancellationToken);

            if (result.Records.Count == 0)
            {
                return NoContent();
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets all reporting plans for a facility, optionally narrowed to a reporting period or
        /// reporting state.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<FacilityReportingPlanModel>))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("facilities/{facilityId}")]
        public async Task<IActionResult> GetFacilityReportingPlansForFacility(string facilityId, int? month, int? year,
            bool? isReporting, CancellationToken cancellationToken)
        {
            facilityId = facilityId.Sanitize();

            var periodError = ValidatePeriodFilters(month, year);
            if (periodError is not null)
            {
                return BadRequest(periodError);
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Reporting Plans For Facility");

            var results = await _queries.GetForFacilityAsync(facilityId, month, year, isReporting, cancellationToken);

            if (results.Count == 0)
            {
                return NoContent();
            }

            return Ok(results);
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
            if (request is null)
            {
                return BadRequest("A facility reporting plan is required.");
            }

            FacilityReportingPlan created;

            try
            {
                created = await _manager.CreateAsync(ToEntity(request), cancellationToken);
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

            var model = ToModel(created);

            return Created($"/api/dmrp/reporting-plans/{model.Id}", model);
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

            if (request is null)
            {
                return BadRequest("A facility reporting plan is required.");
            }

            if (request.Id != null && request.Id != id)
            {
                return BadRequest("Id in the URL must match the Id in the request body.");
            }

            try
            {
                await _manager.UpdateAsync(id, ToEntity(request), cancellationToken);
            }
            catch (DmrpNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
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
        /// Deletes every facility reporting plan.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete]
        public async Task<IActionResult> DeleteFacilityReportingPlans(CancellationToken cancellationToken)
        {
            try
            {
                var removed = await _manager.DeleteAllAsync(cancellationToken);

                _logger.LogInformation("Deleted all {Count} facility reporting plan(s)", removed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityReportingPlansController.DeleteFacilityReportingPlans");
                return Problem("An error occurred while deleting the facility reporting plans", null, 500);
            }

            return NoContent();
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
            catch (DmrpNotFoundException ex)
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

        /// <summary>
        /// Deletes every reporting plan belonging to a facility.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("facilities/{facilityId}")]
        public async Task<IActionResult> DeleteFacilityReportingPlansForFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = facilityId.Sanitize();

            try
            {
                await _manager.DeleteForFacilityAsync(facilityId, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityReportingPlansController.DeleteFacilityReportingPlansForFacility");
                return Problem("An error occurred while deleting the facility reporting plans", null, 500);
            }

            return NoContent();
        }

        private static string? ValidatePeriodFilters(int? month, int? year)
        {
            if (month is < 1 or > 12)
            {
                return "month must be between 1 and 12.";
            }

            if (year is < FacilityReportingPlanManager.MinimumReportingYear
                or > FacilityReportingPlanManager.MaximumReportingYear)
            {
                return $"year must be between {FacilityReportingPlanManager.MinimumReportingYear} and " +
                       $"{FacilityReportingPlanManager.MaximumReportingYear}.";
            }

            return null;
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static FacilityReportingPlan ToEntity(FacilityReportingPlanModel model) => new()
        {
            FacilityId = model.FacilityId?.Sanitize() ?? string.Empty,
            MeasureMappingId = model.MeasureMappingId?.Sanitize() ?? string.Empty,
            ReportingMonth = model.ReportingMonth,
            ReportingYear = model.ReportingYear,
            IsReporting = model.IsReporting
        };

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) => new()
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            MeasureMappingId = entity.MeasureMappingId,
            ReportingMonth = entity.ReportingMonth,
            ReportingYear = entity.ReportingYear,
            IsReporting = entity.IsReporting,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}
