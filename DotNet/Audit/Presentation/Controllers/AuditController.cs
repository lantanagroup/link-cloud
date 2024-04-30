using LantanaGroup.Link.Audit.Application.Audit.Queries;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.Audit.Presentation.Controllers
{
    [Route("api/audit")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly ILogger<AuditController> _logger;
        private readonly IAuditFactory _auditFactory;
        private readonly IGetAuditEventListQuery _getAuditEventListQuery;
        private readonly IGetFacilityAuditEventsQuery _getFacilityAuditEventsQuery;
        private readonly IGetAuditEventQuery _getAuditEventQuery;        
        private readonly IAuditServiceMetrics _auditServiceMetrics;

        private int maxAuditEventsPageSize = 20;

        public AuditController(ILogger<AuditController> logger, IAuditHelper auditHelper, IAuditFactory auditFactory, IGetAuditEventQuery getAuditEventQuery, IGetAuditEventListQuery getAuditEventListQuery, IAuditServiceMetrics auditServiceMetrics, IGetFacilityAuditEventsQuery getFacilityAuditEventsQuery)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditFactory = auditFactory ?? throw new ArgumentNullException(nameof(auditFactory));
            _getAuditEventListQuery = getAuditEventListQuery ?? throw new ArgumentNullException(nameof(getAuditEventListQuery));
            _getAuditEventQuery = getAuditEventQuery ?? throw new ArgumentNullException(nameof(getAuditEventQuery));
            _auditServiceMetrics = auditServiceMetrics ?? throw new ArgumentNullException(nameof(auditServiceMetrics));
            _getFacilityAuditEventsQuery = getFacilityAuditEventsQuery ?? throw new ArgumentNullException(nameof(getFacilityAuditEventsQuery));
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

                AuditSearchFilterRecord searchFilter = _auditFactory.CreateAuditSearchFilterRecord(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, sortOrder, pageSize, pageNumber);
                _logger.LogAuditEventListQuery(searchFilter);

                //Get list of audit events using supplied filters and pagination
                PagedAuditModel auditEventList = await _getAuditEventListQuery.Execute(searchFilter, HttpContext.RequestAborted);

                if(auditEventList == null || !(auditEventList.Records.Count > 0)) { return NoContent(); }

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers["X-Pagination"] = JsonSerializer.Serialize(auditEventList.Metadata);
                                
                return Ok(auditEventList);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.RecordException(ex);
                AuditSearchFilterRecord searchFilter = _auditFactory.CreateAuditSearchFilterRecord(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, sortOrder, pageSize, pageNumber);
                _logger.LogAuditEventListQueryException(ex.Message, searchFilter);
                throw;
            }
                    
        }        

        /// <summary>
        /// Returns an audit event with the provided Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuditModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuditModel>> GetAuditEvent(Guid id)
        {
            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("audit-log.id", id);

            if (id == Guid.Empty) { return BadRequest("No audit event id provided."); }
            _logger.LogGetAuditEventById(id.ToString());

            try
            {
                AuditModel auditEvent = await _getAuditEventQuery.Execute(new AuditId(id), HttpContext.RequestAborted);                

                if (auditEvent == null) { return NotFound(); }
                
                return Ok(auditEvent);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.RecordException(ex);
                ex.Data.Add("audit-log.id", id);
                _logger.LogGetAuditEventByIdException(id.ToString(), ex.Message);
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

                //Get list of audit events using supplied filters and pagination
                PagedAuditModel auditEventList = await _getFacilityAuditEventsQuery.Execute(facilityId, sortBy, sortOrder, pageNumber, pageSize, HttpContext.RequestAborted);

                if (auditEventList == null || !(auditEventList.Records.Count > 0)) { return NoContent(); }

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers["X-Pagination"] = JsonSerializer.Serialize(auditEventList.Metadata);

                return Ok(auditEventList);
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
