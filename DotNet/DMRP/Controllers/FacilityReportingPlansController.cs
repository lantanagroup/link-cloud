using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
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
        private readonly IFacilityReportingPlanLookAhead _lookAhead;
        private readonly IFacilityExistence _facilityExistence;
        private readonly IDmrpReportingPlanSync _sync;
        private readonly TimeProvider _timeProvider;

        public FacilityReportingPlansController(ILogger<FacilityReportingPlansController> logger, IFacilityReportingPlanManager manager, IFacilityReportingPlanQueries queries, IFacilityReportingPlanLookAhead lookAhead, IDmrpReportingPlanSync sync, IFacilityExistence facilityExistence, TimeProvider timeProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _lookAhead = lookAhead ?? throw new ArgumentNullException(nameof(lookAhead));
            _facilityExistence = facilityExistence ?? throw new ArgumentNullException(nameof(facilityExistence));
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>
        /// Gets a paged list of every facility reporting plan.
        /// </summary>
        /// <remarks>
        /// A reporting plan records what DMRP said a facility is enrolled to report for one measure in
        /// one reporting period. This is the unfiltered form of <c>GET search</c>.
        /// </remarks>
        /// <param name="sortBy">
        /// Column to sort by: Id, FacilityId, MeasureMappingId, ReportingMonth, ReportingYear,
        /// IsReporting, CreateDate or ModifyDate. Defaults to Id. Any other value is refused.
        /// </param>
        /// <param name="sortOrder">Ascending or Descending. Defaults to Descending.</param>
        /// <param name="pageSize">Rows per page, 1 to 100. Defaults to 10.</param>
        /// <param name="pageNumber">One-based page number. Defaults to 1.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">A page of reporting plans with its paging metadata.</response>
        /// <response code="400">
        /// sortBy names a column that cannot be sorted on, or a paging argument is outside the
        /// supported range. Out-of-range paging is refused rather than quietly clamped, so a caller
        /// that asks for page -1 is told its request was wrong instead of being handed page 1.
        /// </response>
        /// <response code="500">The reporting plans could not be read.</response>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedFacilityReportingPlanDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet(Name = "GetFacilityReportingPlans")]
        public Task<IActionResult> GetFacilityReportingPlans(string? sortBy, SortOrder? sortOrder,
            int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default) =>
            SearchFacilityReportingPlans(new FacilityReportingPlanSearchFilters(), sortBy, sortOrder,
                pageSize, pageNumber, cancellationToken);

        /// <summary>
        /// Searches facility reporting plans by any combination of facility, measure mapping, reporting
        /// period and reporting state.
        /// </summary>
        /// <remarks>
        /// Every filter is optional and they combine with AND, so supplying none returns everything.
        /// Use <c>facilityId</c> with <c>month</c> and <c>year</c> to answer "what is this facility
        /// enrolled to report this period", which is the question the scheduling workflow asks.
        /// </remarks>
        /// <param name="filters">
        /// Optional filters. facilityId and measureMappingId match exactly; month is 1 to 12; year is
        /// 2000 to 2100; isReporting selects only enrolled (true) or only withdrawn (false) entries.
        /// </param>
        /// <param name="sortBy">
        /// Column to sort by: Id, FacilityId, MeasureMappingId, ReportingMonth, ReportingYear,
        /// IsReporting, CreateDate or ModifyDate. Defaults to Id. Any other value is refused.
        /// </param>
        /// <param name="sortOrder">Ascending or Descending. Defaults to Descending.</param>
        /// <param name="pageSize">Rows per page, 1 to 100. Defaults to 10.</param>
        /// <param name="pageNumber">One-based page number. Defaults to 1.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">
        /// A page of matching reporting plans with its paging metadata. A search that matches nothing
        /// is an empty page, not a 404.
        /// </response>
        /// <response code="400">
        /// month or year is outside its range, sortBy names a column that cannot be sorted on, or a
        /// paging argument is out of range.
        /// </response>
        /// <response code="500">The reporting plans could not be read.</response>
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
        /// <remarks>
        /// Unpaged: a facility holds one row per measure per period, so the result stays small. This is
        /// the read behind the Admin UI's per-facility reporting plan view.
        /// </remarks>
        /// <param name="facilityId">The reporting facility, as the Tenant service knows it (the NHSN Org Id).</param>
        /// <param name="month">Optional reporting month, 1 to 12. Omit to return every month.</param>
        /// <param name="year">Optional reporting year, 2000 to 2100. Omit to return every year.</param>
        /// <param name="isReporting">
        /// Optional. True returns only measures the facility is enrolled in, false only those it has
        /// withdrawn from. Omit to return both.
        /// </param>
        /// <param name="monthsAhead">
        /// Optional look-ahead of 1 to 24 reporting periods, counting the current one. Cannot be
        /// combined with month or year - a request that supplies both is refused rather than resolved
        /// by a precedence rule the caller would have to know.
        /// </param>
        /// <param name="refresh">
        /// Asks DMRP for the facility's plan before answering, so the response reflects what DMRP
        /// says now rather than what Link last recorded. Refreshes the period the request is about -
        /// month and year when given, otherwise the current one.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">
        /// The matching reporting plans. A facility with none, or one that does not exist, returns an
        /// empty list rather than a 404 - absence of enrollment is a meaningful answer here.
        /// </response>
        /// <response code="400">
        /// month or year is outside its range, monthsAhead is outside 1 to 24, or monthsAhead was
        /// combined with month or year.
        /// </response>
        /// <response code="502">
        /// refresh was asked for and DMRP could not be read. Nothing stale is served in its place:
        /// a caller that asked for current data is told it did not get any.
        /// </response>
        /// <response code="500">The reporting plans could not be read.</response>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<FacilityReportingPlanModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("facilities/{facilityId}")]
        public async Task<IActionResult> GetFacilityReportingPlansForFacility(string facilityId, int? month, int? year,
            bool? isReporting, int? monthsAhead, bool refresh, CancellationToken cancellationToken)
        {
            facilityId = facilityId.Sanitize();

            var periodError = ValidatePeriodFilters(month, year) ?? ValidateLookAhead(monthsAhead, month, year);
            if (periodError is not null)
            {
                return BadRequestProblem(periodError);
            }

            // The period the request is about. An exact month or year names it; without one the
            // current period is the only thing the request can mean.
            var current = CurrentPeriod();

            var refreshFailure = await RefreshAsync(refresh, facilityId,
                new ReportingPeriod(year ?? current.Year, month ?? current.Month), cancellationToken);

            if (refreshFailure is not null)
            {
                return refreshFailure;
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Reporting Plans For Facility");

            var results = await _queries.GetForFacilityAsync(facilityId, month, year, isReporting,
                LookAheadWindow(monthsAhead, current), cancellationToken);

            return Ok(results);
        }

        /// <summary>
        /// Gets a facility's reporting plan as a calendar: one entry per reporting period, carrying the
        /// measures the facility is enrolled to report in it.
        /// </summary>
        /// <remarks>
        /// This is the read behind the facility-facing reporting plan table. It answers the same
        /// question as <c>GET facilities/{facilityId}</c> but at the grain the facility reads in - a
        /// period with its measures, rather than a row per measure - so the table renders from one
        /// call with no client-side grouping or join to the measure mappings.
        /// <para>
        /// The look-ahead is anchored on the current month in UTC. A facility whose local month has
        /// already turned over sees the window start one month behind for those few hours; the
        /// facility's own timezone is not available to this module, and the reporting workflow reads
        /// its period from that timezone, so the two can disagree at a month boundary.
        /// </para>
        /// </remarks>
        /// <param name="facilityId">The reporting facility, as the Tenant service knows it (the NHSN Org Id).</param>
        /// <param name="monthsAhead">
        /// Optional look-ahead of 1 to 24 reporting periods, counting the current one -
        /// <c>monthsAhead=6</c> is this month and the next five. Omit to return every period the
        /// facility has a plan for, past ones included.
        /// </param>
        /// <param name="isReporting">
        /// Optional. Defaults to true, so the response is what the facility is currently obliged to
        /// report. Pass false for the measures it has withdrawn from, which are kept rather than
        /// deleted.
        /// </param>
        /// <param name="refresh">
        /// Asks DMRP for the facility's plan before answering. Refreshes the current period, which is
        /// the enrollment every projected month in the window is derived from - so one refresh makes
        /// the whole look-ahead current, rather than one call per month in it.
        /// </param>
        /// <param name="pageSize">Periods per page, 1 to 100. Defaults to 10.</param>
        /// <param name="pageNumber">One-based page number. Defaults to 1.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">
        /// A page of reporting periods in chronological order, oldest first, with paging metadata that
        /// counts periods rather than plan rows. A facility with no plans, or one that does not exist,
        /// returns an empty records array rather than a 404.
        /// </response>
        /// <response code="400">
        /// monthsAhead is outside 1 to 24, or a paging argument is out of range.
        /// </response>
        /// <response code="502">
        /// refresh was asked for and DMRP could not be read. Nothing stale is served in its place:
        /// a caller that asked for current data is told it did not get any.
        /// </response>
        /// <response code="500">The reporting plan could not be read.</response>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedFacilityReportingPlanPeriodDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("facilities/{facilityId}/periods", Name = "GetFacilityReportingPlanPeriods")]
        public async Task<IActionResult> GetFacilityReportingPlanPeriods(string facilityId, int? monthsAhead,
            bool? isReporting, bool refresh = false, int pageSize = 10, int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            facilityId = facilityId.Sanitize();

            var lookAheadError = ValidateLookAhead(monthsAhead, null, null);
            if (lookAheadError is not null)
            {
                return BadRequestProblem(lookAheadError);
            }

            var pagingError = ValidatePaging(pageSize, pageNumber);
            if (pagingError is not null)
            {
                return BadRequestProblem(pagingError);
            }

            // One reading for the whole request. The refresh, the window and the period the answer is
            // anchored on all have to mean the same month, and taking the clock more than once lets a
            // request that spans midnight on the last of the month disagree with itself.
            var anchor = CurrentPeriod();

            var refreshFailure = await RefreshAsync(refresh, facilityId, anchor, cancellationToken);

            if (refreshFailure is not null)
            {
                return refreshFailure;
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Reporting Plan Periods");

            var result = await _lookAhead.GetAsync(facilityId, LookAheadWindow(monthsAhead, anchor), anchor,
                isReporting ?? true, pageSize, pageNumber, cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Gets a facility reporting plan by Id.
        /// </summary>
        /// <param name="id">The reporting plan's own identifier, as returned by create or search.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">The reporting plan.</response>
        /// <response code="404">No reporting plan has that Id.</response>
        /// <response code="500">The reporting plan could not be read.</response>
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
        /// <remarks>
        /// Normally these rows are written by the scheduling workflow from what the DMRP API returns.
        /// This endpoint exists so an operator or test can seed them directly.
        /// <para>
        /// A plan is unique on facility, measure mapping, month and year. The uniqueness is enforced by
        /// a database index as well as a pre-check, so two concurrent writers cannot both get through.
        /// </para>
        /// </remarks>
        /// <param name="request">
        /// The plan to create. facilityId must name a facility that exists, measureMappingId a measure
        /// mapping that exists, reportingMonth 1 to 12 and reportingYear 2000 to 2100. Set isReporting
        /// false to record that a facility has stopped reporting a measure rather than deleting the row,
        /// which keeps the history of what DMRP said and when it changed.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="201">The created plan, with a Location header pointing at it.</response>
        /// <response code="400">
        /// A required field is missing, a value is out of range, or the facility or measure mapping
        /// does not exist.
        /// </response>
        /// <response code="409">
        /// A plan already exists for that facility, measure mapping and reporting period.
        /// </response>
        /// <response code="500">The reporting plan could not be created.</response>
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
        /// <remarks>
        /// Update only. A plan that does not exist is not created here; the response is 404.
        /// </remarks>
        /// <param name="id">The reporting plan to replace.</param>
        /// <param name="request">
        /// The replacement values. id is required in the body and must equal the id in the URL. All
        /// other fields are validated exactly as they are on create.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="202">The updated plan.</response>
        /// <response code="400">
        /// The body has no id, its id does not match the URL, a value is out of range, or the facility
        /// or measure mapping does not exist.
        /// </response>
        /// <response code="404">No reporting plan has that Id.</response>
        /// <response code="409">
        /// The change would collide with an existing plan for that facility, measure mapping and
        /// reporting period.
        /// </response>
        /// <response code="500">The reporting plan could not be updated.</response>
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
        /// <remarks>
        /// Clears the whole table for every facility. Intended for resetting a test environment; there
        /// is no confirmation step and no undo. To clear one facility use
        /// <c>DELETE facilities/{facilityId}</c> instead.
        /// </remarks>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="204">The plans were deleted. Deleting an empty table also succeeds.</response>
        /// <response code="500">The reporting plans could not be deleted.</response>
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
        /// <remarks>
        /// Removes the row outright. To record that a facility has stopped reporting a measure while
        /// keeping the history, set isReporting to false through update instead.
        /// </remarks>
        /// <param name="id">The reporting plan to delete.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="204">The plan was deleted.</response>
        /// <response code="404">No reporting plan has that Id.</response>
        /// <response code="500">The reporting plan could not be deleted.</response>
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
        /// <remarks>
        /// This is what runs when a facility is deleted outright, after the facility itself is gone. A
        /// soft-deleted facility keeps its plans, because it can be restored and the rows are the
        /// record of what it was enrolled to report while it was active.
        /// </remarks>
        /// <param name="facilityId">The facility whose plans are removed.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="204">
        /// The facility's plans were deleted. A facility with none, or one that does not exist, also
        /// succeeds - the endpoint is idempotent.
        /// </response>
        /// <response code="400">facilityId is missing or blank.</response>
        /// <response code="500">The reporting plans could not be deleted.</response>
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

        /// <summary>
        /// Caps the look-ahead. Twenty-four periods is well past anything a facility plans against,
        /// and an uncapped window would let one request group every row a facility has.
        /// </summary>
        internal const int MaximumMonthsAhead = 24;

        /// <summary>
        /// A window and an exact period are two different questions, so a request that asks both is
        /// refused rather than answered by whichever one the implementation happens to apply first.
        /// </summary>
        private static string? ValidateLookAhead(int? monthsAhead, int? month, int? year)
        {
            if (monthsAhead is null)
            {
                return null;
            }

            if (monthsAhead < 1 || monthsAhead > MaximumMonthsAhead)
            {
                return $"monthsAhead must be between 1 and {MaximumMonthsAhead}.";
            }

            if (month is not null || year is not null)
            {
                return "monthsAhead cannot be combined with month or year.";
            }

            return null;
        }

        /// <summary>
        /// The look-ahead window, anchored on the given reporting period. Null when the caller did
        /// not ask for one, which reads as "every period".
        /// </summary>
        /// <remarks>
        /// The anchor is passed in rather than read here so that a caller needing both the window and
        /// the anchor gets one clock reading for the two. Reading it twice lets a request that spans
        /// midnight on the last of the month build a window starting one month after the anchor it is
        /// answered against.
        /// </remarks>
        private static ReportingPeriodRange? LookAheadWindow(int? monthsAhead, ReportingPeriod anchor)
        {
            if (monthsAhead is null)
            {
                return null;
            }

            return ReportingPeriodRange.LookAhead(anchor, monthsAhead.Value);
        }

        /// <summary>
        /// Brings the facility's plan for a period up to date from DMRP before it is read.
        /// </summary>
        /// <remarks>
        /// Returns null when there is nothing to report and the read should go ahead, or the result
        /// to answer with when the refresh failed.
        /// <para>
        /// A failed refresh fails the request rather than falling back to what Link already had.
        /// Serving stale rows to a caller that asked for current ones, with nothing in the response
        /// to say so, is the one outcome this parameter exists to prevent - and a caller that would
        /// rather have stale data than none can simply not ask for a refresh.
        /// </para>
        /// </remarks>
        private async Task<IActionResult?> RefreshAsync(bool refresh, string facilityId, ReportingPeriod period,
            CancellationToken cancellationToken)
        {
            if (!refresh)
            {
                return null;
            }

            // The reads themselves answer for an unknown facility with an empty list, because absence
            // of enrollment is a meaningful answer. A refresh is not a read: it writes reporting plan
            // rows keyed on this id, and the sync documents that the facility was already known when
            // it was asked for -- nothing else establishes that. Without this, a mistyped id whose
            // value DMRP happens to recognise silently creates rows for a facility Link has no record
            // of, and only an explicit delete would ever remove them.
            if (!await _facilityExistence.ExistsAsync(facilityId, cancellationToken))
            {
                return Problem(
                    $"Facility {facilityId} was not found, so its reporting plan cannot be refreshed.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found");
            }

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Refresh Facility Reporting Plan");

            try
            {
                await _sync.SyncAsync(facilityId, period.Month, period.Year, cancellationToken);

                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DmrpApiException ex)
            {
                _logger.LogError(ex,
                    "Refreshing the reporting plan for facility {FacilityId} from DMRP failed",
                    facilityId.SanitizeForLog());

                return Problem(
                    "The reporting plan could not be refreshed from DMRP. Ask again without refresh to read "
                    + "what Link last recorded.",
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Bad Gateway");
            }
        }

        /// <summary>
        /// The reporting period the service considers current, in UTC rather than the facility's
        /// timezone: this module knows whether a facility exists, not where it is. See the remarks on
        /// <see cref="GetFacilityReportingPlanPeriods"/>.
        /// </summary>
        private ReportingPeriod CurrentPeriod()
        {
            var now = _timeProvider.GetUtcNow();

            return new ReportingPeriod(now.Year, now.Month);
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
