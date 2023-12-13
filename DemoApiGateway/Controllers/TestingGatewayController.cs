using LantanaGroup.Link.DemoApiGateway.Application.Commands;
using LantanaGroup.Link.DemoApiGateway.Application.Commands.CreateDataAcquisitionRequestedEvent;
using LantanaGroup.Link.DemoApiGateway.Application.Commands.CreatePatientEvent;
using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/testing")]
    [ApiController]
    public class TestingGatewayController : ControllerBase
    {
        private readonly ILogger<TestingGatewayController> _logger;
        private readonly IOptions<GatewayConfig> _gatewayConfig;
        private readonly ICreateReportScheduledCommand _createReportScheduledCommand;
        private readonly ICreatePatientEventCommand _createPatientEventCommand;
        private readonly ICreateDataAcquisitionRequestedEventCommand _createDataAcquisitionRequestedEventCommand;

        public TestingGatewayController(ILogger<TestingGatewayController> logger, IOptions<GatewayConfig> gatewayConfig, 
            ICreateReportScheduledCommand createReportScheduledCommand,
            ICreatePatientEventCommand createPatientEventCommand, 
            ICreateDataAcquisitionRequestedEventCommand createDataAcquisitionRequestedEventCommand) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));
            _createReportScheduledCommand = createReportScheduledCommand ?? throw new ArgumentNullException(nameof(_createReportScheduledCommand));
            _createPatientEventCommand = createPatientEventCommand ?? throw new ArgumentNullException(nameof(createPatientEventCommand));
            _createDataAcquisitionRequestedEventCommand = createDataAcquisitionRequestedEventCommand ?? throw new ArgumentNullException(nameof(createDataAcquisitionRequestedEventCommand));
        }

        /// <summary>
        /// Create a new report scheduled event
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("report-scheduled")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> CreateReportScheduledEvent(ReportScheduled model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No report scheduled event provided."); }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                var apiEx = new ArgumentNullException("Facility Id was not provided in the new report scheduled event.");
                apiEx.Data.Add("report-scheduled", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Report Scheduled Event"), apiEx, apiEx.Message);
                return BadRequest("Facility Id is required in order to create a report scheduled event.");
            }

            if (string.IsNullOrWhiteSpace(model.ReportType))
            {
                var apiEx = new ArgumentNullException("Patient Id was not provided in the new report scheduled event.");
                apiEx.Data.Add("report-scheduled", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Report Scheduled Event"), apiEx, apiEx.Message);
                return BadRequest("Patient Id is required in order to create a report scheduled event.");
            }

            if (model.StartDate is null || model.EndDate is null)
            {
                var apiEx = new ArgumentNullException("A report period was not provided in the new report scheduled event. Must supply a valid start and end date.");
                apiEx.Data.Add("report-scheduled", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Report Scheduled Event"), apiEx, apiEx.Message);
                return BadRequest("A report period is required in order to create a report scheduled event.");
            }

            try
            {

                string correlationId = await _createReportScheduledCommand.Execute(model);
                EntityActionResponse response = new EntityActionResponse($"The patient event was created succcessfully with a correlation id of '{correlationId}'.", correlationId);

                return Ok(response);

            }
            catch (Exception ex)
            {
                ex.Data.Add("patient-event", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), ex, "An exception occurred while attempting to create a patient event");
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        /// Create a new patient event
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("patient-event")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> CreatePatientEvent(PatientEvent model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification provided."); }

            if (string.IsNullOrWhiteSpace(model.Key))
            {               
                var apiEx = new ArgumentNullException("Facility Id was not provided in the new patient event.");
                apiEx.Data.Add("patient-event", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), apiEx, apiEx.Message);
                return BadRequest("Facility Id is required in order to create a patient event.");
            }

            if (string.IsNullOrWhiteSpace(model.PatientId))
            {
                var apiEx = new ArgumentNullException("Patient Id was not provided in the new patient event.");
                apiEx.Data.Add("patient-event", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), apiEx, apiEx.Message);                
                return BadRequest("Patient Id is required in order to create a patient event.");
            }

            if (string.IsNullOrWhiteSpace(model.EventType))
            {
                var apiEx = new ArgumentNullException("Event Type was not provided in the new patient event.");
                apiEx.Data.Add("patient-event", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), apiEx, apiEx.Message);     
                return BadRequest("An event type is required in order to create a patient event.");
            }           

            try
            {

                string correlationId = await _createPatientEventCommand.Execute(model);
                EntityActionResponse response = new EntityActionResponse($"The patient event was created succcessfully with a correlation id of '{correlationId}'.", correlationId);

                return Ok(response);

            }
            catch (Exception ex)
            {
                ex.Data.Add("patient-event", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), ex, "An exception occurred while attempting to create a patient event");
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        /// Create a new data acquisition requested event
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("data-acquisition-requested-event")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> CreateDataAcquisitionRequestedEvent(DataAcquisitionRequested model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification provided."); }

            if (string.IsNullOrWhiteSpace(model.Key))
            {
                var apiEx = new ArgumentNullException("A Facility Id was not provided in the new data acquisition event.");
                apiEx.Data.Add("data-acquisition-requested", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), apiEx, apiEx.Message);               
                return BadRequest("Facility Id is required in order to create a patient event.");
            }

            if (string.IsNullOrWhiteSpace(model.PatientId))
            {
                var apiEx = new ArgumentNullException("Patient Id was not provided in the new data acquisition event.");
                apiEx.Data.Add("data-acquisition-requested", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), apiEx, apiEx.Message);
                return BadRequest("Patient Id is required in order to create a patient event.");
            }

            if (!(model.reports.Count > 0))
            {
                var apiEx = new ArgumentNullException("Scheduled reports were not provided in the new data acquisition event.");
                apiEx.Data.Add("data-acquisition-requested", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Patient Event"), apiEx, apiEx.Message);               
                return BadRequest("Scheduled reports is required in order to create a patient event.");
            }

            try
            {
                string correlationId = await _createDataAcquisitionRequestedEventCommand.Execute(model);
                EntityActionResponse response = new EntityActionResponse($"The data acquisition requested event was created succcessfully with a correlation id of '{correlationId}'.", correlationId);

                return Ok(response);

            }
            catch (Exception ex)
            {
                ex.Data.Add("data-acquisition-requested", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create Data AcquisitionEvent"), ex, "An exception occurred while attempting to create a data acquisition requested event.");
                return StatusCode(500, ex);
            }
        }
    }
}
