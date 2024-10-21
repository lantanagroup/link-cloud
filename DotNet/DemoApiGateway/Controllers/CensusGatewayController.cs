using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.census;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using LantanaGroup.Link.Census.Application.Models;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/census")]
    [ApiController]
    public class CensusGatewayController : ControllerBase
    {
        private readonly ILogger<CensusGatewayController> _logger;
        private readonly ICensusService _censusService;

        public CensusGatewayController(ILogger<CensusGatewayController> logger, ICensusService censusService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _censusService = censusService ?? throw new ArgumentNullException(nameof(censusService));
        }

        /// <summary>
        /// Create a new census config
        /// </summary>
        /// <param name="censusConfig"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CensusConfigModel censusConfig)
        {
            if (censusConfig == null)
            {
                return BadRequest();
            }

            var response = await _censusService.CreateCensus(censusConfig);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    //var result = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    var result = new EntityActionResponse() { Id = "temp", Message = "Census Configuration created." };
                    return Ok(result);                    
                }

                return StatusCode(500, "An error occurred while attempting to create a new census configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("censusConfig", censusConfig);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a census configuration"), ex, "An exception occurred while attempting to create a census configuration.");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Delete a census config
        /// </summary>
        /// <param name="censusId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("config/{censusId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(string censusId)
        {
            if (string.IsNullOrWhiteSpace(censusId))
            {
                return BadRequest();
            }

            var response = await _censusService.DeleteCensus(censusId);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Census Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a census configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("censusId", censusId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a census configuration"), ex, "An exception occurred while attempting to delete a census configuration.");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Get a census config
        /// </summary>
        /// <param name="censusId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("config/{censusId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Get(string censusId)
        {
            if (string.IsNullOrWhiteSpace(censusId))
            {
                return BadRequest();
            }

            var response = await _censusService.GetCensus(censusId);

            try
            {
                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
                {
                    var result = await response.Content.ReadFromJsonAsync<CensusConfigModel>();
                    return Ok(result);
                }
                else if(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
                {
                    return NotFound();
                }

                return StatusCode(500, "An error occurred while attempting to get a census configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("censusId", censusId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a census configuration"), ex, "An exception occurred while attempting to get a census configuration.");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Update a census config
        /// </summary>
        /// <param name="censusId"></param>
        /// <param name="censusConfig"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("config/{censusId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(string censusId, [FromBody] CensusConfigModel censusConfig)
        {
            if (string.IsNullOrWhiteSpace(censusId))
            {
                return BadRequest();
            }

            if (censusConfig == null)
            {
                return BadRequest();
            }

            var response = await _censusService.UpdateCensus(censusId, censusConfig);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Census Configuration updated." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to update a census configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("censusId", censusId);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a census configuration"), ex, "An exception occurred while attempting to update a census configuration.");
                return StatusCode(500);
            }
        }

    }
}
