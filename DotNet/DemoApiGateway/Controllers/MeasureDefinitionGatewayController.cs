using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/measure")]
    [ApiController]
    public class MeasureDefinitionGatewayController : ControllerBase
    {

        private readonly ILogger<MeasureDefinitionGatewayController> _logger;
        private readonly IMeasureDefinitionService _measureDefinitionService;



        public MeasureDefinitionGatewayController(ILogger<MeasureDefinitionGatewayController> logger, IMeasureDefinitionService measureDefinitionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _measureDefinitionService = measureDefinitionService ?? throw new ArgumentNullException(nameof(measureDefinitionService));
        }


        [HttpGet]
        [Route("config/measures")]
        [ProducesResponseType(typeof(List<MeasureDefinitionConfigModel>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ListMeasureDefinitions()
        {

            try
            {
                var response = await _measureDefinitionService.GetMeasureDefinitions();

                if (response.IsSuccessStatusCode)
                {
                    //TODO : add pagination metadata to response tenant service method
                    var result = await response.Content.ReadFromJsonAsync<List<MeasureDefinitionConfigModel>>();

                    if (result == null) { return NotFound(); }

                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to list of measure definitions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(LoggingIds.ListItems, "List measure definitions"), ex, "An exception occurred while attempting to list of measure definitions");
                return StatusCode(500, ex);
            }
        }


        // GET api/MeasureDefinitionGatewayController>/5
        /// <summary>
        /// Get a measure definition configuration
        /// </summary>
        /// param name="measureDefinitionId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("config/{measureDefinitionId}")]
        public async Task<IActionResult> Get(string measureDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(measureDefinitionId))
            {
                return BadRequest();
            }

            var response = await _measureDefinitionService.GetMeasureDefinition(measureDefinitionId);

            try
            {
                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
                {
                    var result = await response.Content.ReadFromJsonAsync<MeasureDefinitionConfigModel>();
                    return Ok(result);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
                {
                    return NotFound();
                }

                return StatusCode(500, "An error occurred while attempting to get a measure definition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("measureDefinitionId", measureDefinitionId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a measure definition configuration"), ex, "An exception occurred while attempting to get a measure definitio configuration.");
                return StatusCode(500);
            }
        }

        // POST api/<MeasureDefinitionGatewayController>
        /// <summary>
        /// Create a new measure definition config
        /// </summary>
        /// param name="measureDefinitionConfigModel"></param>
        /// <returns>
        ///     200: Success
        ///     401: Unauthorized
        ///     403: Forbidden
        ///     500: Server Error
        ///     400: Bad Request
        /// </returns>

        [HttpPost("config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] MeasureDefinitionConfigModel measureDefinitionConfig)
        {
            if (measureDefinitionConfig == null)
            {
                return BadRequest();
            }

            var response = await _measureDefinitionService.CreateMeasureDefinition(measureDefinitionConfig);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Measure definition  Configuration created." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to create a new measure definition  configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("measureDefinitionConfig", measureDefinitionConfig);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a measure definition configuration"), ex, "An exception occurred while attempting to create a measure definition configuration.");
                return StatusCode(500);
            }
        }

        // PUT api/<MeasureDefinitionGatewayController>/5
        /// <summary>
        /// Updates a measure definition configuration
        /// </summary>
        ///<param name="measureDefinitionConfig"></param>
        ///<param name="measureDefinitionId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("config/{measureDefinitionId}")]
        public async Task<IActionResult> Update(string measureDefinitionId, [FromBody] MeasureDefinitionConfigModel measureDefinitionConfig)
        {

            if (string.IsNullOrWhiteSpace(measureDefinitionId))
            {
                return BadRequest();
            }

            if (measureDefinitionId == null)
            {
                return BadRequest();
            }

            var response = await _measureDefinitionService.UpdateMeasureDefinition(measureDefinitionId, measureDefinitionConfig);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = measureDefinitionId, Message = "Measure Definition Configuration updated." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to update a measure definition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("measureDefinitionId", measureDefinitionId);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a measure definition configuration"), ex, "An exception occurred while attempting to update a measure definitions configuration.");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Delete a measure definition config
        /// </summary>
        /// <param name="measureDefinitionId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("config/{measureDefinitionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(string measureDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(measureDefinitionId))
            {
                return BadRequest();
            }

            var response = await _measureDefinitionService.DeleteMeasureDefinition(measureDefinitionId);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Measure Definition Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a measure definition configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("measureDefinitionId", measureDefinitionId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a measure definition configuration"), ex, "An exception occurred while attempting to delete a measure definition configuration.");
                return StatusCode(500);
            }
        }
    }
}
