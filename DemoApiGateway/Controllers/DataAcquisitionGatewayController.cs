using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition;
using LantanaGroup.Link.DemoApiGateway.Services.Client.DataAcquisition;
using Microsoft.AspNetCore.Mvc;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/data")]
    [ApiController]
    public class DataAcquisitionGatewayController : ControllerBase
    {
        private readonly ILogger<DataAcquisitionGatewayController> _logger;
        private readonly IDataAcquisitionService _dataAcquisitionService;

        public DataAcquisitionGatewayController(ILogger<DataAcquisitionGatewayController> logger, IDataAcquisitionService dataAcquisitionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataAcquisitionService = dataAcquisitionService ?? throw new ArgumentNullException(nameof(dataAcquisitionService));
        }

        #region Data Acquisition Configuration

        /// <summary>
        ///  Create a new data acquisition configuration
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("config")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateDataAcquisitionConfig([FromBody] DataAcquisitionConfigModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.CreateDataAcquisitionConfiguration(model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Configuration created." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to create a new data acquisition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("dataAcquisitionConfig", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a data acquisition configuration"), ex, "An error occurred while attempting to create a new data acquisition configuration.");
                return StatusCode(500, "An error occurred while attempting to create a new data acquisition configuration.");
            }
        }

        /// <summary>
        /// Update a data acquisition configuration
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
        [HttpPut("config/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDataAcquisitionConfig(string facilityId, [FromBody] DataAcquisitionConfigModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.UpdateDataAcquisitionConfiguration(facilityId, model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Configuration updated." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to update a data acquisition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("dataAcquisitionConfig", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a data acquisition configuration"), ex, "An error occurred while attempting to update a data acquisition configuration.");
                return StatusCode(500, "An error occurred while attempting to update a data acquisition configuration.");
            }
        }

        /// <summary>
        /// Get a data acquisition configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("config/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataAcquisitionConfig(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.GetDataAcquisitionConfiguration(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to get a data acquisition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a data acquisition configuration"), ex, "An error occurred while attempting to get a data acquisition configuration.");
                return StatusCode(500, "An error occurred while attempting to get a data acquisition configuration.");
            }
        }

        /// <summary>
        /// Delete a data acquisition configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("config/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDataAcquisitionConfig(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.DeleteDataAcquisitionConfiguration(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a data acquisition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a data acquisition configuration"), ex, "An error occurred while attempting to delete a data acquisition configuration.");
                return StatusCode(500, "An error occurred while attempting to delete a data acquisition configuration.");
            }
        }

        #endregion

        #region Data Acquisition Query Configuration

        /// <summary>
        /// Create a new data acquisition query configuration
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("query/config")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateDataAcquisitionQueryConfig([FromBody] DataAcquisitionQueryConfigModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }
                      
            try
            {
                var response = await _dataAcquisitionService.CreateDataAcquisitionQueryConfig(model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Query Configuration created." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to create a new data acquisition query configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("dataAcquisitionQueryConfig", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a data acquisition query configuration"), ex, "An error occurred while attempting to create a new data acquisition query configuration.");
                return StatusCode(500, "An error occurred while attempting to create a new data acquisition query configuration.");
            }
        }

        /// <summary>
        /// Update a data acquisition query configuration
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
        [HttpPut("query/{facilityId}/config")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDataAcquisitionQueryConfig(string facilityId, [FromBody] DataAcquisitionQueryConfigModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.UpdateDataAcquisitionQueryConfig(facilityId, model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Query Configuration updated." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to update a data acquisition query configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("dataAcquisitionQueryConfig", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a data acquisition query configuration"), ex, "An error occurred while attempting to update a data acquisition query configuration.");
                return StatusCode(500, "An error occurred while attempting to update a data acquisition query configuration.");
            }
        }

        /// <summary>
        /// Get a data acquisition query configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("query/{facilityId}/config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataAcquisitionQueryConfig(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }                       

            try
            {
                var response = await _dataAcquisitionService.GetDataAcquisitionQueryConfig(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DataAcquisitionQueryConfigModel>();
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to get a data acquisition query configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a data acquisition query configuration"), ex, "An error occurred while attempting to get a data acquisition query configuration.");
                return StatusCode(500, "An error occurred while attempting to get a data acquisition query configuration.");
            }
        }

        /// <summary>
        /// Delete a data acquisition query configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("query/{facilityId}/config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDataAcquisitionQueryConfig(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.DeleteDataAcquisitionQueryConfig(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Query Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a data acquisition query configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a data acquisition query configuration"), ex, "An error occurred while attempting to delete a data acquisition query configuration.");
                return StatusCode(500, "An error occurred while attempting to delete a data acquisition query configuration.");
            }
        }

        #endregion

        #region Data Acquisition Authentication Configuration

        /// <summary>
        /// Create a new data acquisition authentication configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryType"></param>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("auth/{facilityId}/config/{queryType}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateDataAcquisitionAuthConfig(string facilityId, QueryConfigurationTypePathParameter queryType, [FromBody] AuthenticationConfiguration model)
        {
            if (model == null)
            {
                return BadRequest();
            }                       

            try
            {

                var response = await _dataAcquisitionService.CreateDataAcquisitionAuthenticationConfiguration(facilityId, queryType, model);

                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Authentication Configuration created." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to create a new data acquisition authentication configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("dataAcquisitionAuthConfig", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a data acquisition authentication configuration"), ex, "An error occurred while attempting to create a new data acquisition authentication configuration.");
                return StatusCode(500, "An error occurred while attempting to create a new data acquisition authentication configuration.");
            }
        }

        /// <summary>
        /// Update a data acquisition authentication configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryType"></param>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("auth/{facilityId}/config/{queryType}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDataAcquisitionAuthConfig(string facilityId, QueryConfigurationTypePathParameter queryType, [FromBody] AuthenticationConfiguration model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.UpdateDataAcquisitionAuthenticationConfiguration(facilityId, queryType, model);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Authentication Configuration updated." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to update a data acquisition authentication configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("dataAcquisitionAuthConfig", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a data acquisition authentication configuration"), ex, "An error occurred while attempting to update a data acquisition authentication configuration.");
                return StatusCode(500, "An error occurred while attempting to update a data acquisition authentication configuration.");
            }
        }

        /// <summary>
        /// Get a data acquisition authentication configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryType"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("auth/{facilityId}/config/{queryType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataAcquisitionAuthConfig(string facilityId, QueryConfigurationTypePathParameter queryType)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            var response = await _dataAcquisitionService.GetDataAcquisitionAuthenticationConfiguration(facilityId, queryType);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AuthenticationConfiguration>();
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to get a data acquisition authentication configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a data acquisition authentication configuration"), ex, "An error occurred while attempting to get a data acquisition authentication configuration.");
                return StatusCode(500, "An error occurred while attempting to get a data acquisition authentication configuration.");
            }
        }

        /// <summary>
        /// Delete a data acquisition authentication configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryType"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("auth/{facilityId}/config/{queryType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDataAcquisitionAuthConfig(string facilityId, QueryConfigurationTypePathParameter queryType)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.DeleteDataAcquisitionAuthenticationConfiguration(facilityId, queryType);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Authentication Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a data acquisition authentication configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a data acquisition authentication configuration"), ex, "An error occurred while attempting to delete a data acquisition authentication configuration.");
                return StatusCode(500, "An error occurred while attempting to delete a data acquisition authentication configuration.");
            }
        }

        #endregion

        #region Data Acquisition Query Plan

        /// <summary>
        /// Create a new data acquisition query plan
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryPlanType"></param>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("query/{facilityId}/plan/{queryPlanType}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateDataAcquisitionQueryPlan(string facilityId, string queryPlanType, [FromBody] DataAcquisitionQueryPlanModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.CreateDataAcquisitionQueryPlan(facilityId, queryPlanType, model);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Query Plan Configuration created." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to create a new data acquisition query plan.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("queryPlanType", queryPlanType);
                ex.Data.Add("dataAcquisitionQueryPlan", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a data acquisition query plan"), ex, "An error occurred while attempting to create a new data acquisition query plan.");
                return StatusCode(500, "An error occurred while attempting to create a new data acquisition query plan.");
            }
        }

        /// <summary>
        /// Update a data acquisition query plan
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryPlanType"></param>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("query/{facilityId}/plan/{queryPlanType}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDataAcquisitionQueryPlan(string facilityId, string queryPlanType, [FromBody] DataAcquisitionQueryPlanModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.UpdateDataAcquisitionQueryPlan(facilityId, queryPlanType, model);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Query Plan Configuration updated." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to update a data acquisition query plan.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("queryPlanType", queryPlanType);
                ex.Data.Add("dataAcquisitionQueryPlan", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a data acquisition query plan"), ex, "An error occurred while attempting to update a data acquisition query plan.");
                return StatusCode(500, "An error occurred while attempting to update a data acquisition query plan.");
            }
        }

        /// <summary>
        /// Get a data acquisition query plan
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryPlanType"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("query/{facilityId}/plan/{queryPlanType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataAcquisitionQueryPlan(string facilityId, string queryPlanType)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.GetDataAcquisitionQueryPlan(facilityId, queryPlanType);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DataAcquisitionQueryPlanModel>();
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to get a data acquisition query plan.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("queryPlanType", queryPlanType);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a data acquisition query plan"), ex, "An error occurred while attempting to get a data acquisition query plan.");
                return StatusCode(500, "An error occurred while attempting to get a data acquisition query plan.");
            }
        }

        /// <summary>
        /// Delete a data acquisition query plan
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="queryPlanType"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("query/{facilityId}/plan/{queryPlanType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDataAcquisitionQueryPlan(string facilityId, string queryPlanType)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }            

            try
            {
                var response = await _dataAcquisitionService.DeleteDataAcquisitionQueryPlan(facilityId, queryPlanType);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Data Acquisition Query Plan Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a data acquisition query plan.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("queryPlanType", queryPlanType);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a data acquisition query plan"), ex, "An error occurred while attempting to delete a data acquisition query plan.");
                return StatusCode(500, "An error occurred while attempting to delete a data acquisition query plan.");
            }
        }

        #endregion

    }
}
