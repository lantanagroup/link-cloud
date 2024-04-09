using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/tenant")]
    [ApiController]
    public class TenantGatewayController : ControllerBase
    {
        private readonly ILogger<AuditGatewayController> _logger;  
        private readonly ITenantService _tenantService;

        private int maxItemsPageSize = 20;

        public TenantGatewayController(ILogger<AuditGatewayController> logger, ITenantService tenantService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        }

        /// <summary>
        /// Create a new facility
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost]
        [Route("facility")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CreateTenant([FromBody] FacilityConfigModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }           

            try 
            {
                var response = await _tenantService.CreateFacility(model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Facility Config Created." };
                    return Ok(result);
                    
                }

                return StatusCode(500, "An error occurred while attempting to create a new tenant.");
            }
            catch(Exception ex)
            {
                ex.Data.Add("tenant", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create Tenant"), ex, "An exception occurred while attempting to create a new tenant");
                return StatusCode(500, ex);
            }
            

            
        }

        /// <summary>
        /// Update a facility
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut]
        [Route("facility/{facilityId}")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdateFacility(string facilityId, [FromBody] FacilityConfigModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _tenantService.UpdateFacility(facilityId, model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Facility Config Updated." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to update a facility.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("facility", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update Facility"), ex, "An exception occurred while attempting to update a facility");
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        /// Delete a facility
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete]
        [Route("facility/{facilityId}")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> DeleteFacility(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _tenantService.DeleteFacility(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Facility Config Deleted." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to delete a facility.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete Facility"), ex, "An exception occurred while attempting to delete a facility");
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        /// Get a facility by Id
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [Route("facility/{facilityId}")]
        [ProducesResponseType(typeof(FacilityConfigModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetFacility(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _tenantService.GetFacility(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FacilityConfigModel>();
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to get a facility.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get Facility"), ex, "An exception occurred while attempting to get a facility");
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        /// Get a list of facilities
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="facilityName"></param>
        /// <param name="sortBy"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [Route("facility")]
        [ProducesResponseType(typeof(List<FacilityConfigModel>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ListFacilities(string? facilityId, string? facilityName, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //make sure page size does not exceed the max page size allowed
            if (pageSize > maxItemsPageSize) { pageSize = maxItemsPageSize; }
            if (pageNumber < 1) { pageNumber = 1; }                       

            try
            {
                var response = await _tenantService.ListFacilities(facilityId, facilityName, sortBy, pageSize, pageNumber);

                if (response.IsSuccessStatusCode)
                {
                    //TODO : add pagination metadata to response tenant service method
                    var result = await response.Content.ReadFromJsonAsync<List<FacilityConfigModel>>();

                    if (result == null) { return NotFound(); }

                    //add X-Pagination header for machine-readable pagination metadata
                    //Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(result.Metadata));

                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to list facilities.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("facilityName", facilityName);
                _logger.LogError(new EventId(LoggingIds.ListItems, "List Facilities"), ex, "An exception occurred while attempting to list facilities");
                return StatusCode(500, ex);
            }
        }
    }
}
