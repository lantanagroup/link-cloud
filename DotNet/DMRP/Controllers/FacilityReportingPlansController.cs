using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Mapping;
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
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilityReportingPlans")]
        public Task<IActionResult> GetFacilityReportingPlans(string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default) =>
            SearchFacilityReportingPlans(new FacilityReportingPlanSearchFilters(), sortBy, sortOrder,
                pageSize, pageNumber, cancellationToken);

        /// <summary>
        /// Search facility reporting plans by any combination of facility, measure mapping, reporting
        /// period and reporting state.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedFacilityReportingPlanDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("search", Name = "SearchFacilityReportingPlans")]
        public async Task<IActionResult> SearchFacilityReportingPlans([FromQuery] FacilityReportingPlanSearchFilters filters,
            string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var facilityId = NullIfBlank(filters.FacilityId?.Sanitize());
            var measureMappingId = NullIfBlank(filters.MeasureMappingId?.Sanitize());

            sortBy = NullIfBlank(sortBy?.Sanitize());

            var periodError = ValidatePeriodFilters(filters.Month, filters.Year);
            if (periodError is not null)
            {
                return BadRequestProblem(periodError);
            }

            if (sortBy is not null && !SortableColumns.Contains(sortBy))
            {
                return BadRequestProblem($"Cannot sort by '{sortBy}'.");
            }

            var pagingError = ValidatePaging(pageSize, pageNumber);
            if (pagingError is not null)
            {
                return BadRequestProblem(pagingError);
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Search Facility Reporting Plans");

            var result = await _queries.PagedSearchAsync(facilityId, measureMappingId,
                filters.Month, filters.Year, filters.IsReporting, sortBy ?? nameof(FacilityReportingPlan.Id),
                sortOrder ?? SortOrder.Descending, pageSize, pageNumber, cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Gets all reporting plans for a facility, optionally narrowed to a reporting period or
        /// reporting state.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<FacilityReportingPlanModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("facilities/{facilityId}")]
        public async Task<IActionResult> GetFacilityReportingPlansForFacility(string facilityId, int? month, int? year,
            bool? isReporting, CancellationToken cancellationToken)
        {
            facilityId = facilityId.Sanitize();

            var periodError = ValidatePeriodFilters(month, year);
            if (periodError is not null)
            {
                return BadRequestProblem(periodError);
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Reporting Plans For Facility");

            var results = await _queries.GetForFacilityAsync(facilityId, month, year, isReporting, cancellationToken);

            return Ok(results);
        }

        /// <summary>
        /// Gets a facility reporting plan by Id.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityReportingPlanModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFacilityReportingPlan(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            var model = await _queries.GetAsync(id, cancellationToken);

            if (model == null)
            {
                return NotFoundProblem($"Facility reporting plan with Id: {id} not found.");
            }

            return Ok(model);
        }

        /// <summary>
        /// Creates a facility reporting plan.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityReportingPlanModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> CreateFacilityReportingPlan(FacilityReportingPlanRequest request, CancellationToken cancellationToken)
        {
            FacilityReportingPlan created;

            try
            {
                created = await _manager.CreateAsync(ToEntity(request), cancellationToken);
            }
            catch (DuplicateReportingPlanException ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
            }
            catch (ReportingPlanValidationException ex)
            {
                return BadRequestProblem(ex.Message);
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
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFacilityReportingPlan(string id, FacilityReportingPlanUpdateRequest request, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            var requestId = request.Id?.Sanitize();

            if (string.IsNullOrWhiteSpace(requestId))
            {
                return BadRequestProblem("Id is required in the request body.");
            }

            if (requestId != id)
            {
                return BadRequestProblem("Id in the URL must match the Id in the request body.");
            }

            try
            {
                await _manager.UpdateAsync(id, ToEntity(request), cancellationToken);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundProblem(ex.Message);
            }
            catch (DuplicateReportingPlanException ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
            }
            catch (ReportingPlanValidationException ex)
            {
                return BadRequestProblem(ex.Message);
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
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFacilityReportingPlan(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            try
            {
                await _manager.DeleteAsync(id, cancellationToken);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundProblem(ex.Message);
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
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("facilities/{facilityId}")]
        public async Task<IActionResult> DeleteFacilityReportingPlansForFacility(string facilityId, CancellationToken cancellationToken)
        {
            facilityId = NullIfBlank(facilityId.Sanitize());

            if (facilityId is null)
            {
                return BadRequestProblem("FacilityId is required.");
            }

            try
            {
                await _manager.DeleteForFacilityAsync(facilityId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityReportingPlansController.DeleteFacilityReportingPlansForFacility");
                return Problem("An error occurred while deleting the facility reporting plans", null, 500);
            }

            return NoContent();
        }

        /// <summary>
        /// Rejects paging arguments outside the supported range rather than quietly substituting the
        /// defaults, so a caller that asks for page -1 is told its request was wrong instead of being
        /// handed page 1 as though it had asked for it.
        /// </summary>
        internal const int MaximumPageSize = 100;

        private static string? ValidatePaging(int pageSize, int pageNumber)
        {
            if (pageNumber < 1)
            {
                return "pageNumber must be 1 or greater.";
            }

            if (pageSize < 1 || pageSize > MaximumPageSize)
            {
                return $"pageSize must be between 1 and {MaximumPageSize}.";
            }

            return null;
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

        private ObjectResult BadRequestProblem(string detail) =>
            Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");

        private ObjectResult NotFoundProblem(string detail) =>
            Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static FacilityReportingPlan ToEntity(FacilityReportingPlanRequest request) =>
            FacilityReportingPlanMapper.ToEntity(request);

        private static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) =>
            FacilityReportingPlanMapper.ToModel(entity);
    }
}
