﻿using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Commands.Config;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Serializers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
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
        public async Task<IActionResult> StoreTenant([FromBody]NormalizationConfigModel config)
        {
            if(config == null)
            {
                return BadRequest("No request body found.");
            }

            try
            {
                await _mediator.Send(new SaveConfigEntityCommand
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
                _logger.LogError(ex.Message, ex);
                return BadRequest(ex.Message);
            }
            catch(EntityAlreadyExistsException ex) 
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest($"Entity for {config?.FacilityId} already exists. Please use PUT request to update.");
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
                await _mediator.Send(new SaveConfigEntityCommand
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
                _logger.LogError(ex.Message, ex);
                return BadRequest(ex.Message);
            }
            catch (EntityAlreadyExistsException ex)
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest($"Entity for {config?.FacilityId} already exists. Please use PUT request to update.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
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
                await _mediator.Send(new DeleteConfigCommand
                {
                    FacilityId = facilityId
                });
            }
            catch (ConfigOperationNullException ex)
            {
                _logger.LogError(ex.Message, ex);
                return BadRequest(ex.Message);
            }
            catch (NoEntityFoundException ex)
            {
                _logger.LogError(ex.Message, ex);
                return NotFound(ex.Message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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
                auditEvent.Resource = nameof(NormalizationConfig);
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
