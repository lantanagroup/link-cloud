using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.notification;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using static LantanaGroup.Link.DemoApiGateway.settings.GatewayConstants;

namespace LantanaGroup.Link.DemoApiGateway.Controllers
{
    [Route("api/notification")]
    [ApiController]
    public class NotificationGatewayController : ControllerBase
    {
        private readonly ILogger<NotificationGatewayController> _logger;
        private readonly INotificationService _notificationService;

        private int maxAuditEventsPageSize = 20;
        private int maxNotificationConfigurationPageSize = 20;

        public NotificationGatewayController(ILogger<NotificationGatewayController> logger, INotificationService notificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));             
        }

        /// <summary>
        /// List notifications
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="filterFacilityBy"></param>
        /// <param name="filterNotificationTypeBy"></param>
        /// <param name="createdOnStart"></param>
        /// <param name="createdOnEnd"></param>
        /// <param name="sentOnStart"></param>
        /// <param name="sentOnEnd"></param>
        /// <param name="sortBy"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Not Found: 404
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedNotificationModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<PagedNotificationModel>> ListNotifications(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //TODO check for authorization

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxAuditEventsPageSize) { pageSize = maxAuditEventsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                //Get list of audit events using supplied filters and pagination
                HttpResponseMessage response = await _notificationService.ListNotifications(searchText, filterFacilityBy, filterNotificationTypeBy, createdOnStart, createdOnEnd, sentOnStart, sentOnEnd, sortBy, pageSize, pageNumber);
                if (response.IsSuccessStatusCode)
                {
                    var notificationtList = await response.Content.ReadFromJsonAsync<PagedNotificationModel>();

                    if (notificationtList == null) { return NotFound(); }

                    //add X-Pagination header for machine-readable pagination metadata
                    Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(notificationtList.Metadata));

                    return Ok(notificationtList);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(LoggingIds.ListItems, "List Notifiations"), ex, "An exception occurred while attempting to retrieve notifications.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Create a new notification
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
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> CreateNotificationAsync(NotificationMessage model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification provided."); }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                var apiEx = new ArgumentNullException("Facility Id was not provided order to create a notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Notification"), apiEx, apiEx.Message);                          
            }

            if (string.IsNullOrWhiteSpace(model.Subject))
            {
                var apiEx = new ArgumentNullException("Notification subject was not provided order to create a notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Notification"), apiEx, apiEx.Message);
                return BadRequest("Notification subject is required in order to create a notification.");
            }

            if (string.IsNullOrWhiteSpace(model.Body))
            {
                var apiEx = new ArgumentNullException("Notification body was not provided in order to create a notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Notification"), apiEx, apiEx.Message);
                return BadRequest("A message is required in order to create a notification.");
            }

            if (model.Recipients is null || model.Recipients.Count == 0)
            {
                var apiEx = new ArgumentNullException("At least one recipient is required in order to create a notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Notification"), apiEx, apiEx.Message);
                return BadRequest("At least one recipient is required in order to create a notification.");
            }

            try
            {
                //Create notification
                HttpResponseMessage response = await _notificationService.CreateNotification(model);
                if (response.IsSuccessStatusCode)
                {
                    var entityCreated = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    return Ok(entityCreated);
                }

                return StatusCode(500, "An error occurred while attempting to create a new notification.");

            }
            catch (Exception ex)
            {
                ex.Data.Add("notification", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create Notifiation"), ex, "An exception occurred while attempting to create a new notification");
                return StatusCode(500, ex);
            }
        }

        #region Facility Configuration

        /// <summary>
        /// List notification configurations
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="filterFacilityBy"></param>
        /// <param name="sortBy"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Not Found: 404
        ///     Server Error: 500
        /// </returns>
        [HttpGet("configuration")]
        [ProducesResponseType(typeof(PagedNotificationConfigurationModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<PagedNotificationConfigurationModel>> ListConfigurations(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //TODO check for authorization

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationConfigurationPageSize) { pageSize = maxNotificationConfigurationPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                //Get list of audit events using supplied filters and pagination
                HttpResponseMessage response = await _notificationService.ListConfigurations(searchText, filterFacilityBy, sortBy, pageSize, pageNumber);
                if (response.IsSuccessStatusCode)
                {
                    var configList = await response.Content.ReadFromJsonAsync<PagedNotificationConfigurationModel>();

                    if (configList == null) { return NotFound(); }

                    //add X-Pagination header for machine-readable pagination metadata
                    Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(configList.Metadata));

                    return Ok(configList);
                }
                else
                {
                    return NotFound();
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(LoggingIds.ListItems, "Get Notifiation Configurations"), ex, $"An exception occurred while attempting to retrieve notification configurations.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Create a new notification configuration
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("configuration")]
        [Authorize(Policy = "CanCreateNotifiactionConfigurations")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> CreateNotificationConfigurationAsync(NotificationConfigurationModel model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification configuration provided."); }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                var apiEx = new ArgumentNullException("Facility Id is required in order to create a notification.");
                apiEx.Data.Add("notification-configuration", model);
                _logger.LogWarning(new EventId(LoggingIds.GenerateItems, "Create Notification Configuration"), apiEx, apiEx.Message);               
                return BadRequest("Facility Id is required in order to create a notification configuration.");
            }

            try
            {
                //Create notification
                HttpResponseMessage response = await _notificationService.CreateNotificationConfiguration(model);
                if (response.IsSuccessStatusCode)
                {
                    var entityCreated = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    return Ok(entityCreated);
                }

                return StatusCode(500, "An error occurred while attempting to create a new notification configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("notification-configuration", model);
                _logger.LogError(new EventId(LoggingIds.GenerateItems, "Create Notifiation Configuration"), ex, "An exception occurred while attempting to create a new notification configuration.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Update a notification configuration
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("configuration")]
        [Authorize(Policy = "CanUpdateNotifiactionConfigurations")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> UpdateNotificationConfigurationAsync(NotificationConfigurationModel model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification configuration provided."); }

            if (model.Id == Guid.Empty)
            {
                _logger.LogError($"Id was not provided in the updated notification configuration: {model}.");
                return BadRequest("No notification configuration id provided.");
            }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                _logger.LogError($"Facility Id was not provided in the new notification configuration: {model}.");
                return BadRequest("Facility Id is required in order to create a notification configuration.");
            }

            try
            {                       
                //update configuration
                HttpResponseMessage response = await _notificationService.UpdateNotificationConfiguration(model);
                if (response.IsSuccessStatusCode)
                {
                    var entityUpdated = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    return Ok(entityUpdated);
                }

                return StatusCode(500, "An error occurred while attempting to update a new notification configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("notification-configuration", model);
                _logger.LogError(new EventId(LoggingIds.UpdateItem, "Update Notifiation Configuration"), ex, "An exception occurred while attempting to updated an existing notification configuration.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Delete a notification configuration
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Reqeust: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("configuration/{id}")]
        [Authorize(Policy = "CanDeleteNotifiactionConfigurations")]
        [Authorize(Policy = "ClientApplicationCanDelete")]
        [ProducesResponseType(typeof(EntityActionResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<EntityActionResponse>> DeleteNotificationConfigurationAsync(Guid id)
        {
            //TODO check for authorization
       
            if (id == null || id == Guid.Empty) { return BadRequest("No configuration id provided."); }

            try
            {
                //delete configuration
                HttpResponseMessage response = await _notificationService.DeleteNotificationConfiguration(id);
                if (response.IsSuccessStatusCode)
                {
                    var entityUpdated = await response.Content.ReadFromJsonAsync<EntityActionResponse>();
                    return Ok(entityUpdated);
                }

                return StatusCode(500, "An error occurred while attempting to update a new notification configuration.");
            }
            catch (Exception ex)
            {
                ex.Data.Add("Notification Configuration Id", id);
                _logger.LogError(new EventId(LoggingIds.DeleteItem, "Delete Notifiation Configuration"), ex, "An exception occurred while attempting to delete a notification configuration.");
                return StatusCode(500, ex);
            }

        }

        #endregion
    }
}
