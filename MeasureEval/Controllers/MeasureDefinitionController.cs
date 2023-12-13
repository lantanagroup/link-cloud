using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Services;
using Microsoft.AspNetCore.Mvc;
using LantanaGroup.Link.MeasureEval.Entities;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Model;
using System.Text.Json;
using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;
using LantanaGroup.Link.MeasureEval.Settings;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LantanaGroup.Link.MeasureEval.Controllers
{
    [Route("api/measureDef/")]
    [ApiController]
    public class MeasureDefinitionController : ControllerBase
    {
        private readonly MeasureEvalConfig _measureEvalConfig;
        private readonly MeasureDefinitionService _measureDefService;
        private readonly IKafkaProducerFactory _kafkaProducerFactory;
        private readonly ILogger<MeasureDefinitionController> _logger;
        private MeasureEvalService _measureEvalService;


        public MeasureDefinitionController(MeasureDefinitionService measureDefService, MeasureEvalConfig measureEvalConfig, MeasureEvalService measureEvalService, ILogger<MeasureDefinitionController> logger, IKafkaProducerFactory kafkaProducerFactory)
        {
            _measureEvalConfig = measureEvalConfig;
            _measureDefService = measureDefService;
            _measureEvalService = measureEvalService;
            _logger = logger;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));

        }

        /// <summary>
        /// Posts a MeasureDefinition for a given reportDefinition.
        /// </summary>
        /// <param name="reportDefinition"></param>
        /// <param name="token"></param>
        /// <returns>
        ///     Success: 201
        ///     Bad Request: 404
        ///     Server Error: 500
        /// </returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StoreMeasureDefinition(ReportDefinition reportDefinition, CancellationToken token)
        {
            if (reportDefinition.Bundle == null && reportDefinition.Url == null)
            {
                return BadRequest("Either a Bundle or an Url must be specified in a JSON body of the request");
            }

            MeasureDefinition measureDefinition = new MeasureDefinition
            {
                measureDefinitionId = reportDefinition.BundleId,
                measureDefinitionName = reportDefinition.BundleName,
                url = reportDefinition.Url
            };
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            var bundleAsString = "";

            if (reportDefinition.Bundle == null)
            {
                measureDefinition.bundle = await _measureDefService.getBundleFromUrl(reportDefinition.Url!);
                bundleAsString = JsonSerializer.Serialize(measureDefinition.bundle, options);
            }
            else
            {         
                try
                {
                    bundleAsString = JsonSerializer.Serialize(reportDefinition.Bundle);
                    measureDefinition.bundle = JsonSerializer.Deserialize<Bundle>(bundleAsString, options);
                    //_logger.LogDebug(bundleAsString);
                }
                catch (DeserializationFailedException ex)
                {
                    _logger.LogError(ex.Message);

                    return BadRequest(ex.Message);
                }
            }

            try
            {

                var response = await _measureEvalService.UpdateMeasure(measureDefinition.measureDefinitionId, bundleAsString);

                if (response.IsSuccessStatusCode)
                {
                    await _measureDefService.CreateMeasureDefinition(measureDefinition, token);

                    await CreateAuditEvent(measureDefinition.measureDefinitionId, AuditEventType.Create, $"Successfully stored measure definition ({measureDefinition.measureDefinitionId}) and sent to {_measureEvalConfig.EvaluationServiceUrl}.", measureDefinition);

                    return CreatedAtAction(nameof(StoreMeasureDefinition), new { id = measureDefinition.Id }, measureDefinition);
                }
                else
                {
                    await CreateAuditEvent(measureDefinition.measureDefinitionId, AuditEventType.Create, $"Error encountered when sent to {_measureEvalConfig.EvaluationServiceUrl}.", measureDefinition);

                    return StatusCode(500, "Error encountered when measure definition is sent to: " + _measureEvalConfig.EvaluationServiceUrl);
                }
            }
            catch (ApplicationException ex)
            {
                _logger.LogError(ex.Message);

                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                await CreateAuditEvent(measureDefinition.measureDefinitionId, AuditEventType.Create, $"Error encountered:{ex.Message}", measureDefinition);

                return StatusCode(500, ex.Message);
            }

        }

        /// <summary>
        /// Updates a MeasureDefinition for a given measureDefId and reportDefinition.
        /// </summary>
        /// <param name="measureDefId"></param>
        /// <param name="reportDefinition"></param>
        /// <param name="token"></param>
        /// <returns>
        ///     Success: 202
        ///     Bad Request: 404
        ///     Server Error: 500
        /// </returns>
        [HttpPut("{measureDefId}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateMeasureDefinition(String measureDefId, [FromBody] ReportDefinition reportDefinition, CancellationToken token)
         {

            if (measureDefId != reportDefinition.BundleId)
            {
                return BadRequest($"The Measure Definition Id passed in url {measureDefId} does not the measure Definition Id passed in the request body {reportDefinition.BundleId}");
            }
            MeasureDefinition measureDefinition = await _measureDefService.GetMeasureDefinition(measureDefId, token);
            if (measureDefinition == null)
            {
                return NotFound("The measure Definition Id/Bundle Id does not exist.");
            }
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            var bundleAsString = "";

            if (reportDefinition.BundleName != null)
            {
                measureDefinition.measureDefinitionName = reportDefinition.BundleName;
            }
            if (reportDefinition.Bundle == null)
            {
                string? url = reportDefinition.Url ?? measureDefinition.url;
                if (url == null)
                {
                    return BadRequest("Either a Bundle or an Url must be specified in a JSON body of the request");
                }
                measureDefinition.url = url;
                measureDefinition.bundle = await _measureDefService.getBundleFromUrl(url);
                bundleAsString = JsonSerializer.Serialize(measureDefinition.bundle, options);
            }
            else
            {
                measureDefinition.url = reportDefinition.Url;
                try
                {
                    bundleAsString = JsonSerializer.Serialize(reportDefinition.Bundle);
                    measureDefinition.bundle = JsonSerializer.Deserialize<Bundle>(bundleAsString, options);
                   //  _logger.LogDebug(bundleAsString);
                }
                catch (DeserializationFailedException ex)
                {
                    _logger.LogError(ex.Message);

                    return BadRequest(ex.Message);
                }
            }

            try
            {

                var response = await _measureEvalService.UpdateMeasure(measureDefId, bundleAsString);

                if (response.IsSuccessStatusCode)
                {

                    await _measureDefService.UpdateMeasureDefinition(measureDefId, measureDefinition, token);

                    await CreateAuditEvent(measureDefId, AuditEventType.Update, $"Successfully updated measure definition ({measureDefId}) and sent to {_measureEvalConfig.EvaluationServiceUrl}.", measureDefinition);

                    return NoContent();
                }
                else
                {
                    await CreateAuditEvent(measureDefId, AuditEventType.Update, $"Error encountered when sent to {_measureEvalConfig.EvaluationServiceUrl}.", measureDefinition);

                    return StatusCode(500, "Error encountered when measure definition is sent to: " + _measureEvalConfig.EvaluationServiceUrl);
                }
            }
            catch (ApplicationException ex)
            {
                _logger.LogError(ex.Message);

                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                await CreateAuditEvent(measureDefId, AuditEventType.Update, $"Error encountered:{ex.Message}", measureDefinition);

                return StatusCode(500, ex.Message);
            }

        }

        /// <summary>
        /// Gets MeasureDefinition for a given a measureDefId.
        /// </summary>
        /// <param name="measureDefId"></param>
        /// <param name="token"></param>
        /// <returns>
        ///     Success: 200
        ///     Server Error: 500
        /// </returns>
        [HttpGet("{measureDefId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MeasureDefinition>> GetMeasureDefinition(String measureDefId, CancellationToken token)
        {
            MeasureDefinition measureDefinition = await _measureDefService.GetMeasureDefinition(measureDefId, token);
            if (measureDefinition == null)
                return NotFound();
            return this.Ok(measureDefinition);
        }



        [HttpDelete("{measureDefId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(string measureDefId, CancellationToken token)

        {
            try
            {
                await _measureDefService.DeleteMeasureDefinition(measureDefId, token);

                await CreateAuditEvent(measureDefId, AuditEventType.Delete,  $"Successfully deleted measure definition ({measureDefId}).");

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                await CreateAuditEvent(measureDefId, AuditEventType.Delete, $"Error encountered:{ex.Message}");

                return StatusCode(500, ex.Message);
            }

        }


        private async Task<string> CreateAuditEvent(String measureDefId, AuditEventType type, String notes, MeasureDefinition? measureDefinition = null) 
        {

            try
            {
                using var producer = _kafkaProducerFactory.CreateAuditEventProducer();
                AuditEventMessage auditEvent = new AuditEventMessage();
                auditEvent.ServiceName = MeasureEvalConstants.AppSettingsSectionNames.ServiceName;
                auditEvent.Id = measureDefId;
                auditEvent.EventDate = DateTime.UtcNow;
                auditEvent.User = "SystemUser";
                auditEvent.Action = type;
                auditEvent.Resource = typeof(MeasureDefinition).Name;
                auditEvent.url = measureDefinition?.url;
                auditEvent.Notes = notes;

                var headers = new Headers();
                headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(Guid.NewGuid().ToString()));

                //write to auditable event occurred topic
                await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                {
                    Key = measureDefId,
                    Value = auditEvent,
                    Headers = headers
                });

                return measureDefId;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to create audit event for {type} of measure definition {measureDefId}.", ex);
            }
        }
    }
}
