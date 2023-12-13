using LantanaGroup.Link.DemoApiGateway.Application.models.audit;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/audit")]
    [ApiController]
    public class AuditGatewayController : ControllerBase
    {
        private readonly ILogger<AuditGatewayController> _logger;
        private readonly IAuditService _auditService;

        private int maxAuditEventsPageSize = 20;

        public AuditGatewayController(ILogger<AuditGatewayController> logger, IAuditService auditService) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// List audit events
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
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Not Found: 404
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        //[Authorize(Roles = "LinkAdministrator")]
        [Authorize(Policy = "UserCanViewAuditLogs")]
        [Authorize(Policy = "ClientApplicationCanRead")]
        [ProducesResponseType(typeof(PagedAuditModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<PagedAuditModel>> ListAuditEvents(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string?
            filterActionBy, Guid? filterUserBy, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //TODO check for authorization
            var userClaims = User.Claims.ToList(); 

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxAuditEventsPageSize) { pageSize = maxAuditEventsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                HttpResponseMessage response = await _auditService.ListAuditEvents(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber); 

                if (response.IsSuccessStatusCode)
                {
                    var auditEventList = await response.Content.ReadFromJsonAsync<PagedAuditModel>();

                    if (auditEventList == null) { return NotFound(); }

                    //add X-Pagination header for machine-readable pagination metadata
                    Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(auditEventList.Metadata));

                    return Ok(auditEventList);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(LoggingIds.ListItems, "List Audit Events"), ex, "An exception occurred while attempting to retrieve audit events.");
                return StatusCode(500, ex);
            }

        }

    }
}
