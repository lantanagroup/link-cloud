using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition;
using LantanaGroup.Link.DemoApiGateway.Application.models.normalization;
using LantanaGroup.Link.DemoApiGateway.Services.Client.Normalization;
using Microsoft.AspNetCore.Mvc;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/normalization")]
    [ApiController]
    public class NormalizationGatewayController : ControllerBase
    {
        private readonly ILogger<NormalizationGatewayController> _logger;
        private readonly INormalizationService _normalizationService;

        public NormalizationGatewayController(INormalizationService normalizationService, ILogger<NormalizationGatewayController> logger)
        {
            _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create a new normalization configuration
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
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateNormalizationConfig(NormalizationConfigModel model)
        {
            if (model == null) { return BadRequest("No configuration provided."); }

            try
            {
                var response = await _normalizationService.CreateNormalizationConfig(model);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Normalization Configuration created." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to create a new normalization configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("normalizationConfig", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create a normalization configuration"), ex, "An error occurred while attempting to create a normalization configuration.");
                return StatusCode(500, "An error occurred while attempting to create a normalization configuration.");
            }            
        }

        /// <summary>
        /// Get a normalization configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNormalizationConfig(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            try
            {
                var response = await _normalizationService.GetNormalizationConfig(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<NormalizationConfigModel>();
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to retrieve a normalization configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.GetItem, "Get a normalization configuration"), ex, "An error occurred while attempting to retrieve a normalization configuration.");
                return StatusCode(500, "An error occurred while attempting to retrieve a normalization configuration.");
            }
        }

        /// <summary>
        /// Update a normalization configuration
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
        [HttpPut("{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateNormalizationConfig(string facilityId, NormalizationConfigModel model)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            try
            {
                var response = await _normalizationService.UpdateNormalizationConfig(facilityId, model);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Normalization Configuration updated." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to update a normalization configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                ex.Data.Add("normalizationConfig", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update a normalization configuration"), ex, "An error occurred while attempting to update a normalization configuration.");
                return StatusCode(500, "An error occurred while attempting to update a normalization configuration.");
            }
        }

        /// <summary>
        /// Delete a normalization configuration
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteNormalizationConfig(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            try
            {
                var response = await _normalizationService.DeleteNormalizationConfig(facilityId);

                if (response.IsSuccessStatusCode)
                {
                    var result = new EntityActionResponse() { Id = "temp", Message = "Normalization Configuration deleted." };
                    return Ok(result);
                }

                return StatusCode(500, "An error occurred while attempting to delete a normalization configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("facilityId", facilityId);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete a normalization configuration"), ex, "An error occurred while attempting to delete a normalization configuration.");
                return StatusCode(500, "An error occurred while attempting to delete a normalization configuration.");
            }
        }

    }
}
