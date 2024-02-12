using LantanaGroup.Link.Audit.Application.Audit.Queries;
using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;
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
        private readonly ICreateAuditEventCommand _createAuditEventCommand;
        private readonly IGetAuditEventListQuery _getAuditEventListQuery;
        private readonly IGetAuditEventQuery _getAuditEventQuery;
        private int maxAuditEventsPageSize = 20;
        private readonly AuditServiceMetrics _auditServiceMetrics;

        public AuditController(ILogger<AuditController> logger, IAuditHelper auditHelper, IAuditFactory auditFactory, ICreateAuditEventCommand createAuditEventCommand, IGetAuditEventQuery getAuditEventQuery, IGetAuditEventListQuery getAuditEventListQuery, AuditServiceMetrics auditServiceMetrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));           
            _auditFactory = auditFactory ?? throw new ArgumentNullException(nameof(auditFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _getAuditEventListQuery = getAuditEventListQuery ?? throw new ArgumentNullException(nameof(getAuditEventListQuery));
            _getAuditEventQuery = getAuditEventQuery ?? throw new ArgumentNullException(nameof(getAuditEventQuery));
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
        /// <param name="sortBy"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedAuditModel))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]        
        public async Task<ActionResult<PagedAuditModel>> ListAuditEvents(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? 
            filterActionBy, string? filterUserBy, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {           
            using var _ = _auditServiceMetrics.MeasureAuditSearchDuration();

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxAuditEventsPageSize) { pageSize = maxAuditEventsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                AuditSearchFilterRecord searchFilter = _auditFactory.CreateAuditSearchFilterRecord(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber);
                _logger.LogAuditEventListQuery(searchFilter);

                //Get list of audit events using supplied filters and pagination
                PagedAuditModel auditEventList = await _getAuditEventListQuery.Execute(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(auditEventList.Metadata));
                                
                return Ok(auditEventList);

            }
            catch (Exception ex)
            {
                AuditSearchFilterRecord searchFilter = _auditFactory.CreateAuditSearchFilterRecord(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber);
                _logger.LogAuditEventListQueryException(ex.Message, searchFilter);
                return StatusCode(500, ex);
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
        public async Task<ActionResult<AuditModel>> GetAuditEvent(string id)
        {
            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("audit-id", id);

            if (string.IsNullOrEmpty(id)) { return BadRequest("No audit event id provided."); }
            _logger.LogGetAuditEventById(id);

            try
            {
                AuditModel auditEvent = await _getAuditEventQuery.Execute(id);                

                if (auditEvent == null) { return NotFound(); }
                
                return Ok(auditEvent);
            }
            catch (Exception ex)
            {
                ex.Data.Add("audit-event-id", id);
                _logger.LogGetAuditEventByIdException(id, ex.Message);
                return StatusCode(500, ex);
            }

        }        
    }
}
