using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Commands.Config;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Serializers;
using LantanaGroup.Link.Normalization.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NormalizationController : ControllerBase
    {
        private readonly ILogger<NormalizationController> _logger;
        private readonly IMediator _mediator;
        private readonly IKafkaProducerFactory<string, Shared.Application.Models.Kafka.AuditEventMessage> _kafkaProducerFactory;

        public NormalizationController(IKafkaProducerFactory<string, Shared.Application.Models.Kafka.AuditEventMessage> kafkaProducerFactory,
            IMediator mediator, ILogger<NormalizationController> logger)
        {
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
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
        public async Task<IActionResult> StoreTenant([FromBody]JsonElement config)
        {
            string? body = string.Empty;
            try
            {
                var element = (JsonElement)config;
                body = element.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (body == null)
            {
                return BadRequest("No request body");
            }

            NormalizationConfigModel? configModel = null;
            try
            {
                configModel = NormalizationConfigModelDeserializer.Deserialize(config);

                await _mediator.Send(new SaveConfigEntityCommand
                {
                    NormalizationConfigModel = configModel,
                    Source = SaveTypeSource.Create
                });
            }
            catch (TenantNotFoundException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ConfigOperationNullException ex) 
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest(ex.Message);
            }
            catch(EntityAlreadyExistsException ex) 
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest($"Entity for {configModel?.FacilityId} already exists. Please use PUT request to update.");
            }
            catch(Exception ex) 
            {
                _logger.LogError(ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
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
        public async Task<ActionResult<NormalizationConfigModel>> GetConfig(string facilityId)
        {
            NormalizationConfigModel config = null;
            try
            {
                config = await _mediator.Send(new GetConfigurationModelQuery
                {
                    FacilityId = facilityId
                });
            }
            catch(NoEntityFoundException ex)
            {
                var message = $"No Facility found for GET facility {facilityId}.";
                _logger.LogError(message, ex);
                return NotFound();
            }
            catch(Exception ex)
            {
                var message = $"Internal Error for GET facility {facilityId}.";
                _logger.LogError(message, ex);
            }
            
            return Ok(config);
        }

        /// <summary>
        /// Update a config.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updatedTenantNormalization"></param>
        /// <returns></returns>
        [HttpPut("{facilityId}")]
        public async Task<IActionResult> UpdateTenantNormalization(string facilityId, [FromBody] JsonElement config)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            string? body = string.Empty;
            try
            {
                var element = (JsonElement)config;
                body = element.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (body == null)
            {
                return BadRequest("No request body");
            }

            NormalizationConfigModel? configModel = null;
            try
            {
                configModel = NormalizationConfigModelDeserializer.Deserialize(config);

                await _mediator.Send(new SaveConfigEntityCommand
                {
                    NormalizationConfigModel = configModel,
                    Source = SaveTypeSource.Update,
                    FacilityId = facilityId,
                });
            }
            catch (TenantNotFoundException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ConfigOperationNullException ex)
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest(ex.Message);
            }
            catch (EntityAlreadyExistsException ex)
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest($"Entity for {configModel?.FacilityId} already exists. Please use PUT request to update.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            //await CreateAuditEvent(configModel, AuditEventType.Create);

            return Accepted();
        }

        /// <summary>
        /// Delete  a config.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns></returns>
        [HttpDelete("{facilityId}")]
        public async Task<IActionResult> DeleteTenantNormalization(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                return BadRequest();

            try
            {
                await _mediator.Send(new DeleteConfigCommand
                {
                    FacilityId = facilityId
                });
            }
            catch(ConfigOperationNullException ex)
            {
                return BadRequest();
            }
            catch(Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Accepted();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<string> CreateAuditEvent(NormalizationConfigModel model, AuditEventType type)
        {
            try
            {
                using var producer = _kafkaProducerFactory.CreateAuditEventProducer();
                Shared.Application.Models.Kafka.AuditEventMessage auditEvent = new Shared.Application.Models.Kafka.AuditEventMessage();
                auditEvent.ServiceName = "Normalization Service";
                auditEvent.EventDate = DateTime.UtcNow;
                //auditEvent.UserId =
                auditEvent.User = "SystemUser";
                auditEvent.Action = type;
                auditEvent.Resource = typeof(NormalizationConfigEntity).Name;
                auditEvent.Notes = $"{type} for normalization configuration ({model.FacilityId})'.";

                var headers = new Headers();
                headers.Add("X-Correlation-Id", (Guid.NewGuid().ToByteArray()));

                //write to auditable event occurred topic
                await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string,Shared.Application.Models.Kafka.AuditEventMessage>
                {
                    Key = model.FacilityId,
                    Value = auditEvent,
                    Headers = headers
                });

                return model.FacilityId;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to create audit event for {type} normalization configuration for tenant {model.FacilityId}.", ex);
            }
        }
    }
}
