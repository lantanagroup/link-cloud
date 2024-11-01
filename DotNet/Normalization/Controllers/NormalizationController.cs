using LantanaGroup.Link.Normalization.Application.Managers;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class NormalizationController : ControllerBase
    {
        private readonly ILogger<NormalizationController> _logger;
        private readonly INormalizationConfigManager _configManager;
        private readonly IKafkaProducerFactory<string, Shared.Application.Models.Kafka.AuditEventMessage> _kafkaProducerFactory;

        public NormalizationController(IKafkaProducerFactory<string, Shared.Application.Models.Kafka.AuditEventMessage> kafkaProducerFactory,
            ILogger<NormalizationController> logger, INormalizationConfigManager configManager)
        {
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Save a config for a facility.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StoreTenant([FromBody]NormalizationConfigModel config)
        {
            if(config == null)
            {
                return BadRequest("No request body found.");
            }

            try
            {
                await _configManager.SaveConfigEntity(new SaveConfigEntityCommand
                {
                    NormalizationConfigModel = config,
                    Source = SaveTypeSource.Create
                });
            }
            catch (TenantNotFoundException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ConfigOperationNullException ex) 
            {
                return BadRequest(ex.Message);
            }
            catch(EntityAlreadyExistsException ex) 
            {
                return BadRequest($"Entity for {config?.FacilityId} already exists. Please use PUT request to update.");
            }
            catch(Exception ex) 
            {
                _logger.LogError(ex, "Exception encountered in NormalizationController.StoreTenant");
                return Problem(
                    detail: "An error occurred while processing your request.",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }

            //await CreateAuditEvent(configModel, AuditEventType.Create);

            return Created(string.Empty, string.Empty);
        }

        /// <summary>
        /// Get a Normalization Config by Facility Id.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns></returns>
        [HttpGet("{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<NormalizationConfig>> GetConfig(string facilityId)
        {
            NormalizationConfig config = null;
            try
            {
                config = await _configManager.SingleOrDefaultAsync(c => c.FacilityId == facilityId);

                if (config == null)
                    throw new NoEntityFoundException($"No Facility found for GET facility {facilityId}.");
            }
            catch(NoEntityFoundException ex)
            {
                return NotFound();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in NormalizationController.GetConfig");
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
            
            return Ok(config);
        }

        /// <summary>
        /// Update the Tenant Normalization Config for the provide Facility ID.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPut("{facilityId}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTenantNormalization(string facilityId, [FromBody] NormalizationConfigModel config)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            if (config == null)
            {
                return BadRequest("No request body");
            }

            try
            {
                await _configManager.SaveConfigEntity(new SaveConfigEntityCommand
                {
                    NormalizationConfigModel = config,
                    Source = SaveTypeSource.Update,
                    FacilityId = facilityId,
                });
            }
            catch (TenantNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ConfigOperationNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (EntityAlreadyExistsException ex)
            {
                return BadRequest($"Entity for {config?.FacilityId} already exists. Please use PUT request to update.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in NormalizationController.UpdateTenantNormalization");
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            return Accepted();
        }

        /// <summary>
        /// Delete the Tenant Normalization Config for the provide Facility ID.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns></returns>
        [HttpDelete("{facilityId}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTenantNormalization(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            try
            {
                await _configManager.DeleteAsync(facilityId);
            }
            catch (ConfigOperationNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (NoEntityFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in NormalizationController.DeleteTenantNormalization");
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            return Accepted();
        }
    }
}
