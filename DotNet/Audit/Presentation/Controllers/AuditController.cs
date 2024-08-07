using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.Audit.Presentation.Controllers
{
    [Route("api/audit")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly ILogger<AuditController> _logger;
        private readonly IAuditRepository _auditRepository;
        private readonly ISearchRepository _searchRepository;  
        private readonly IAuditServiceMetrics _auditServiceMetrics;

        private readonly int maxAuditEventsPageSize = 20;

        public AuditController(ILogger<AuditController> logger, IAuditServiceMetrics auditServiceMetrics, ISearchRepository datastore, IAuditRepository auditRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));                   
            _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
            _searchRepository = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _auditServiceMetrics = auditServiceMetrics ?? throw new ArgumentNullException(nameof(auditServiceMetrics));
        }

        /// <summary>
        /// Returns a list of audit events based on filters provided.
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="filterFacilityBy"></param>
        /// <param name="filterCorrelationBy"></param>
        /// <param name="filterServiceBy"></param>
        /// <param name="filterActionBy"></param>
        /// <param name="filterUserBy"></param>
        /// <param name="sortBy">Options: FacilityId, Action, ServiceName, Resource, CreatedOn</param>
        /// <param name="sortOrder">Ascending = 0, Descending = 1, defaults to Ascending</param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200
        ///     NoContent: 204
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedAuditModel))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]        
        public async Task<ActionResult<PagedAuditModel>> ListAuditEvents(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? 
            filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1)
        {           
            //capture audit search duration metric
            using var _ = _auditServiceMetrics.MeasureAuditSearchDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.PageSize, pageSize)
            ]);

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxAuditEventsPageSize) { pageSize = maxAuditEventsPageSize; }
                if (pageNumber < 1) { pageNumber = 1; }

                AuditSearchFilterRecord searchFilter = new(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, sortOrder, pageSize, pageNumber);
                _logger.LogAuditEventListQuery(searchFilter);

                using Activity? activity = ServiceActivitySource.Instance.StartActivity("Search Audit Events");
                activity?.EnrichWithAuditSearchFilter(searchFilter);

                //Get list of audit events using supplied filters and pagination              
                var (res, metadata) = await _searchRepository.SearchAsync(searchFilter.SearchText, searchFilter.FilterFacilityBy, searchFilter.FilterCorrelationBy,
                    searchFilter.FilterServiceBy, searchFilter.FilterActionBy, searchFilter.FilterUserBy, searchFilter.SortBy, searchFilter.SortOrder,
                    searchFilter.PageSize, searchFilter.PageNumber, HttpContext.RequestAborted);

                if (res is null || !res.Any()) { return NoContent(); }              
             
                List<AuditModel> auditEvents = res.Select(AuditModel.FromDomain).ToList();                    

                PagedAuditModel pagedAuditEvents = new PagedAuditModel(auditEvents, metadata);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers["X-Pagination"] = JsonSerializer.Serialize(metadata);

                return Ok(pagedAuditEvents);
                
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.RecordException(ex);
                AuditSearchFilterRecord searchFilter = new(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, sortOrder, pageSize, pageNumber);
                _logger.LogAuditEventListQueryException(ex.Message, searchFilter);
                throw;
            }                    
        }

        /// <summary>
        /// Returns an audit event with the provided Audit Log Id.
        /// </summary>
        /// <param name="auditId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("{auditId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuditModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuditModel>> GetAuditEvent(Guid auditId)
        {
            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("audit-log.id", auditId);

            if (auditId == Guid.Empty) { return BadRequest("No audit event id provided."); }
            _logger.LogGetAuditEventById(auditId.ToString());

            try
            {                
                var res = await _auditRepository.GetAsync(new AuditId(auditId), true, HttpContext.RequestAborted);

                if (res is null) 
                {
                    return Problem(
                        type: AuditConstants.ProblemTypes.NotFound,
                        title: $"Audit Event Not Found.",
                        statusCode: 404,
                        detail: $"No audit event found with an id of '{auditId}'."                      
                    );
                }              

                return Ok(AuditModel.FromDomain(res));                
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.RecordException(ex);
                ex.Data.Add("audit-log.id", auditId);
                _logger.LogGetAuditEventByIdException(auditId.ToString(), ex.Message);
                throw;
            }

        }

        /// <summary>
        /// Returns a list of audit events for a specific facility.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="sortBy">Options: FacilityId, Action, ServiceName, Resource, CreatedOn</param>
        /// <param name="sortOrder">Ascending = 0, Descending = 1, defaults to Ascending</param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns>
        ///     Success: 200
        ///     NoContent: 204
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("facility/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedAuditModel))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedAuditModel>> GetFacilityAuditEvents(string facilityId, string? sortBy, SortOrder? sortOrder, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxAuditEventsPageSize) { pageSize = maxAuditEventsPageSize; }
                if (pageNumber < 1) { pageNumber = 1; }

                _logger.LogGetFacilityAuditEventsQuery(facilityId);

                //Get list of audit events for the specified facility               
                using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("Get Facility Audit Events",
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.SortBy, sortBy),
                    new KeyValuePair<string, object?>(DiagnosticNames.SortOrder, sortOrder),
                    new KeyValuePair<string, object?>(DiagnosticNames.PageSize, pageSize),
                    new KeyValuePair<string, object?>(DiagnosticNames.PageNumber, pageNumber)
                ]);

                var (res, metadata) = await _auditRepository.GetByFacilityAsync(facilityId, sortBy, sortOrder, pageSize, pageNumber, HttpContext.RequestAborted);

                if (res is null || !res.Any()) 
                {
                    return Problem(
                        type: AuditConstants.ProblemTypes.NotFound,
                        title: $"Facility Audit Events Not Found.",
                        statusCode: 404,
                        detail: $"No audit events found for facility '{facilityId}'."
                    );
                }

                List<AuditModel> auditEvents = res.Select(AuditModel.FromDomain).ToList();

                PagedAuditModel pagedAuditEvents = new PagedAuditModel(auditEvents, metadata);                

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers["X-Pagination"] = JsonSerializer.Serialize(metadata);

                return Ok(pagedAuditEvents);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.RecordException(ex);
                _logger.LogGetFacilityAuditEventsQueryException(facilityId, ex.Message);
                throw;
            }
        }
    }
}
