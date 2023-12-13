using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Presentation.Controllers
{
    [Route("api/notification")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly ILogger<NotificationController> _logger;
        private readonly INotificationConfigurationFactory _configurationFactory;
        private readonly INotificationFactory _notificationFactory;
        private int maxNotificationConfigurationPageSize = 20;
        private int maxNotificationsPageSize = 20;

        //Configuration commands and queries
        private readonly ICreateFacilityConfigurationCommand _createFacilityConfigurationCommand;
        private readonly IUpdateFacilityConfigurationCommand _updateFacilityConfigurationCommand;
        private readonly IFacilityConfigurationExistsQuery _facilityConfigurationExistsQuery;
        private readonly IGetFacilityConfigurationQuery _getFacilityConfigurationQuery;
        private readonly IGetNotificationConfigurationQuery _getNotificationConfigurationQuery;
        private readonly IGetFacilityConfigurationListQuery _getFacilityConfigurationListQuery;
        private readonly IDeleteFacilityConfigurationCommand _deleteFacilityConfigurationCommand;

        //Notification commands and queries
        private readonly ICreateNotificationCommand _createNotificationCommand;
        private readonly ISendNotificationCommand _sendNotificationCommand;
        private readonly IValidateEmailAddressCommand _validateEmailAddressCommand;
        private readonly IGetNotificationQuery _getNotificationQuery;
        private readonly IGetFacilityNotificatonsQuery _getFacilityNotificatonsQuery;
        private readonly IGetNotificationListQuery _getNotificationListQuery;


        public NotificationController(ILogger<NotificationController> logger, INotificationConfigurationFactory configurationFactory, INotificationFactory notificationFactory, ICreateFacilityConfigurationCommand createFacilityConfigurationCommand, 
            IUpdateFacilityConfigurationCommand updateFacilityConfigurationCommand, IFacilityConfigurationExistsQuery facilityConfigurationExistsQuery, IGetFacilityConfigurationQuery getFacilityConfigurationQuery, 
            IGetNotificationConfigurationQuery getNotificationConfigurationQuery, IGetFacilityConfigurationListQuery getFacilityConfigurationListQuery, ICreateNotificationCommand createNotificationCommand,
            ISendNotificationCommand sendNotificationCommand, IValidateEmailAddressCommand validateEmailAddressCommand, IGetNotificationQuery getNotificationQuery, IGetFacilityNotificatonsQuery getFacilityNotificatonsQuery, 
            IGetNotificationListQuery getNotificationListQuery, IDeleteFacilityConfigurationCommand deleteFacilityConfigurationCommand)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
            _configurationFactory = configurationFactory ?? throw new ArgumentNullException(nameof(configurationFactory));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
            _createFacilityConfigurationCommand = createFacilityConfigurationCommand ?? throw new ArgumentNullException(nameof(createFacilityConfigurationCommand));
            _updateFacilityConfigurationCommand = updateFacilityConfigurationCommand ?? throw new ArgumentNullException(nameof(updateFacilityConfigurationCommand));
            _facilityConfigurationExistsQuery = facilityConfigurationExistsQuery ?? throw new ArgumentNullException(nameof(facilityConfigurationExistsQuery));
            _getFacilityConfigurationQuery = getFacilityConfigurationQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationQuery));
            _getNotificationConfigurationQuery = getNotificationConfigurationQuery ?? throw new ArgumentNullException(nameof(getNotificationConfigurationQuery));
            _getFacilityConfigurationListQuery = getFacilityConfigurationListQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationListQuery));
            _deleteFacilityConfigurationCommand = deleteFacilityConfigurationCommand ?? throw new ArgumentNullException(nameof(deleteFacilityConfigurationCommand));

            _createNotificationCommand = createNotificationCommand ?? throw new ArgumentNullException(nameof(createNotificationCommand));
            _sendNotificationCommand = sendNotificationCommand ?? throw new ArgumentNullException(nameof(sendNotificationCommand));
            _validateEmailAddressCommand = validateEmailAddressCommand ?? throw new ArgumentNullException(nameof(validateEmailAddressCommand));
            _getNotificationQuery = getNotificationQuery ?? throw new ArgumentNullException(nameof(getNotificationQuery));
            _getFacilityNotificatonsQuery = getFacilityNotificatonsQuery ?? throw new ArgumentNullException(nameof(getFacilityNotificatonsQuery));
            _getNotificationListQuery = getNotificationListQuery ?? throw new ArgumentNullException(nameof(getNotificationListQuery));
        }

        #region Notifications

        /// <summary>
        /// Creates and sends a notification.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityCreatedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityCreatedResponse>> CreateNotificationAsync(NotificationMessage model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification provided."); }

            if (string.IsNullOrWhiteSpace(model.NotificationType))
            {
                var apiEx = new ArgumentNullException("Notification Type was not provided in the new notification.");
                apiEx.Data.Add("notification", model);            
                _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), apiEx, apiEx.Message);
                return BadRequest("Notification Type is required in order to create a notification.");
            }

            if (string.IsNullOrWhiteSpace(model.Subject))
            {
                var apiEx = new ArgumentNullException("Notification subject was not provided in the new notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), apiEx, apiEx.Message);
                return BadRequest("Notification subject is required in order to create a notification.");
            }

            if (string.IsNullOrWhiteSpace(model.Body))
            {
                var apiEx = new ArgumentNullException("The notification body was not provided in the new notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), apiEx, apiEx.Message);
                return BadRequest("A message is required in order to create a notification.");
            }

            if (model.Recipients is null || model.Recipients.Count == 0)
            {
                var apiEx = new ArgumentNullException("No recipients were provided in the new notification.");
                apiEx.Data.Add("notification", model);
                _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), apiEx, apiEx.Message);
                return BadRequest("At least one recipient is required in order to create a notification.");
            }

            //validate email addresses
            foreach (var recipient in model.Recipients)
            {
                bool isValid = await _validateEmailAddressCommand.Execute(recipient);
                if(!isValid)
                {
                    var invalidEmailEx = new ArgumentException("Invalid email address received.");
                    invalidEmailEx.Data.Add("email-address", recipient);
                    _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), invalidEmailEx, "The email addresss '{email}' is invalid. Please only use valid email addresses.", recipient);
                    return BadRequest($"The email addresss '{recipient}' is invalid. Please only use valid email addresses.");
                }
            }

            if (model.Bcc is not null)
            {
                foreach (var recipient in model.Bcc)
                {
                    bool isValid = await _validateEmailAddressCommand.Execute(recipient);
                    if (!isValid)
                    {
                        var invalidEmailEx = new ArgumentException("Invalid email address received.");
                        invalidEmailEx.Data.Add("email-address", recipient);
                        _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), invalidEmailEx, "The email addresss '{email}' is invalid. Please only use valid email addresses.", recipient);
                        return BadRequest($"The email addresss '{recipient}' is invalid. Please only use valid email addresses.");
                    }
                }
            }            

            try
            {
                //Create notification
                CreateNotificationModel notification = _notificationFactory.CreateNotificationModelCreate(model.NotificationType, model.FacilityId, model.CorrelationId, model.Subject, model.Body, model.Recipients, model.Bcc);
                string id = await _createNotificationCommand.Execute(notification);
                EntityCreatedResponse response = new EntityCreatedResponse("The notification was created succcessfully.", id);

                //send notification
                NotificationModel createdNotification = await _getNotificationQuery.Execute(id);
                SendNotificationModel sendModel = _notificationFactory.CreateSendNotificationModel(createdNotification.Id, createdNotification.Recipients, createdNotification.Bcc, createdNotification.Subject, createdNotification.Body);

                //if a facility based notification, get their configuration and add it to the send model
                if (!string.IsNullOrEmpty(model.FacilityId))
                {
                    NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(model.FacilityId);
                    sendModel.FacilityConfig = config;
                }

                //asynchrounously send the email
                _ = Task.Run(() => _sendNotificationCommand.Execute(sendModel));

                return Ok(response);
            }
            catch (Exception ex)
            {
                ex.Data.Add("model", model);
                _logger.LogError(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification"), ex, "An exception occurred while attempting to create a new notification");                
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        /// Returns a list of notifications based on filters provided.
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
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedNotificationModel))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedNotificationModel>> ListNotifications(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //TODO check for authorization

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationConfigurationPageSize) { pageSize = maxNotificationConfigurationPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getNotificationListQuery.Execute(searchText, filterFacilityBy, filterNotificationTypeBy, createdOnStart, createdOnEnd, sentOnStart, sentOnEnd, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(notificationList.Metadata));

                return Ok(notificationList);

            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(NotificationLoggingIds.ListItems, "Notification Service - List notifications"), ex, "An exception occurred while attempting to retrieve notifications.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Returns a notification event with the provided Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NotificationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<NotificationModel>> GetNotification(string id)
        {
            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("notification id", id);

            //TODO check for authorization

            if (string.IsNullOrEmpty(id)) { return BadRequest("No notification id provided."); }

            try
            {
                NotificationModel notification = await _getNotificationQuery.Execute(id);

                if (notification == null) { return NotFound(); }

                return Ok(notification);
            }
            catch (Exception ex)
            {
                ex.Data.Add("notification id", id);
                _logger.LogError(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification by id"), ex, "An exception occurred while attempting to retrieve the nofitication with an id of '{id}'.", id);
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Returns a list of notifications based on the facility Id and provided filters.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="sortBy"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("facility/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedNotificationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedNotificationModel>> GetFacilityNotifications(string facilityId, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //TODO check for authorization

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("facility id", facilityId);

            if (string.IsNullOrWhiteSpace(facilityId)) { return BadRequest("No facility id provided."); }

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationsPageSize) { pageSize = maxNotificationsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getFacilityNotificatonsQuery.Execute(facilityId, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(notificationList.Metadata));

                return Ok(notificationList);
            }
            catch (Exception ex)
            {
                ex.Data.Add("facility id", facilityId);
                _logger.LogError(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notifications by facility id"), ex, "An exception occurred while attempting to retrieve nofitications with a facility id of '{facilityId}'.", facilityId);
                return StatusCode(500, ex);
            }

        }

        #endregion

        #region Configuration

        /// <summary>
        /// Returns a list of notification configurations based on the provided filters.
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="filterFacilityBy"></param>
        /// <param name="sortBy"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>
        ///     Success: 200     
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("configuration")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedNotificationConfigurationModel))]        
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedNotificationConfigurationModel>> ListConfigurations(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            //TODO check for authorization

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationConfigurationPageSize) { pageSize = maxNotificationConfigurationPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                //Get list of audit events using supplied filters and pagination
                PagedNotificationConfigurationModel configList = await _getFacilityConfigurationListQuery.Execute(searchText, filterFacilityBy, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(configList.Metadata));

                return Ok(configList);

            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(NotificationLoggingIds.ListItems, "Notification Service - List notification configurations"), ex, "An exception occurred while attempting to retrieve notification configurations.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Creates a notification configuration.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPost("configuration")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityCreatedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityCreatedResponse>> CreateNotificationConfigurationAsync(NotificationConfigurationModel model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification configuration provided."); }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                var apiEx = new ArgumentNullException("The Facility Id was not provided in the new notification configuration.");
                apiEx.Data.Add("notification-configuration", model);
                _logger.LogWarning(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification configuration"), apiEx, apiEx.Message);                
                return BadRequest("Facility Id is required in order to create a notification configuration.");
            }

            try
            {              
                //Create notification configuration
                CreateFacilityConfigurationModel config = _configurationFactory.CreateFacilityConfigurationModelCreate(model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                string id = await _createFacilityConfigurationCommand.Execute(config);
                EntityCreatedResponse response = new EntityCreatedResponse("The notification configuration was created succcessfully.", id);

                return Ok(response);
            }
            catch (Exception ex)
            {
                ex.Data.Add("model", model);
                _logger.LogError(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification configuration"), ex, "An exception occurred while attempting to create a new notification configuration");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Updates a notification configuration.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpPut("configuration")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityCreatedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityCreatedResponse>> UpdateNotificationConfigurationAsync(NotificationConfigurationModel model)
        {
            //TODO check for authorization

            //validate config values
            if (model == null) { return BadRequest("No notification configuration provided."); }

            if(string.IsNullOrEmpty(model.Id)) 
            {
                var apiEx = new ArgumentNullException("The Configuration Id was not provided in the updated notification configuration.");
                apiEx.Data.Add("notification-configuration", model);
                _logger.LogWarning(new EventId(NotificationLoggingIds.UpdateItem, "Notification Service - Update notification configuration"), apiEx, apiEx.Message);               
                return BadRequest("No notification configuration id provided.");             
            }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                var apiEx = new ArgumentNullException("The Facility Id was not provided in the updated notification configuration.");
                apiEx.Data.Add("notification-configuration", model);
                _logger.LogWarning(new EventId(NotificationLoggingIds.UpdateItem, "Notification Service - Update notification configuration"), apiEx, apiEx.Message);
                return BadRequest("Facility Id is required in order to create a notification configuration.");
            }

            try
            {
                //check if the configuration exists
                bool exists = await _facilityConfigurationExistsQuery.Execute(model.Id);
                if (!exists) 
                {
                    _logger.LogWarning(new EventId(NotificationLoggingIds.UpdateItem, "Notification Service - Update notification configuration"), "No configuration with the id of {id} was found.", model.Id);
                    return BadRequest($"No configuration with the id of {model.Id} was found."); 
                }

                //Update notification configuration
                UpdateFacilityConfigurationModel config = _configurationFactory.UpdateFacilityConfigurationModelCreate(model.Id, model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                string id = await _updateFacilityConfigurationCommand.Execute(config);
                EntityUpdateddResponse response = new EntityUpdateddResponse("The notification configuration was updated succcessfully.", id);

                return Ok(response);
            }
            catch (Exception ex)
            {
                ex.Data.Add("model", model);
                _logger.LogError(new EventId(NotificationLoggingIds.UpdateItem, "Notification Service - Update notification configuration"), ex, "An exception occurred while attempting to updated an existing notification configuration.");
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Returns a notification configuration with the provided facility Id.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("configuration/facility/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NotificationConfigurationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<NotificationConfigurationModel>> GetFacilityConfiguration(string facilityId)
        {
            //TODO check for authorization

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("facility id", facilityId);

            if (string.IsNullOrEmpty(facilityId))
            {
                var apiEx = new ArgumentNullException("The Facility Id was not provided in the request for the notification configuration.");               
                _logger.LogWarning(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification configuration by facility id"), apiEx, apiEx.Message);                
                return BadRequest("No facility id provided."); 
            }

            try
            {
                NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(facilityId);

                if (config == null) { return NotFound(); }

                return Ok(config);
            }
            catch (Exception ex)
            {
                ex.Data.Add("facility id", facilityId);
                _logger.LogError(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification configuration by facility id"), ex, "An exception occurred while attempting to retrieve the notification configuration of a facility with an id of {facilityId}", facilityId);
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Returns a notification configuration with the provided configuration Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet("configuration/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NotificationConfigurationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<NotificationConfigurationModel>> GetNotificationConfiguration(string id)
        {
            //TODO check for authorization

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("notification configuration id", id);

            if (string.IsNullOrEmpty(id)) 
            {
                _logger.LogWarning(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification configuration by id"), "No configuration id provided while attempting to retrieve a configuration by id.");
                return BadRequest("No configuration id provided."); 
            }

            try
            {
                NotificationConfigurationModel config = await _getNotificationConfigurationQuery.Execute(id);

                if (config == null) { return NotFound(); }

                return Ok(config);
            }
            catch (Exception ex)
            {
                ex.Data.Add("configuration id", id);
                _logger.LogError(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification configuration by id"), ex, "An exception occurred while attempting to retrieve the notification configuration of a facility with an id of {id}", id);
                return StatusCode(500, ex);
            }

        }

        /// <summary>
        /// Deletes a notification configuration with the provided configuration Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        ///     Success: 200
        ///     Bad Request: 400
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpDelete("configuration/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityDeletedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityDeletedResponse>> DeleteNotificationConfiguration(string id)
        {
            //TODO check for authorization

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("notification id", id);

            if (string.IsNullOrEmpty(id)) 
            {
                _logger.LogWarning(new EventId(NotificationLoggingIds.DeleteItem, "Notification Service - Delete notification configuration"), "No configuration id provided while attempting to delete a configuration.");
                return BadRequest("No configuration id provided."); 
            }

            try
            {
                bool result = await _deleteFacilityConfigurationCommand.Execute(id);                    
                
                EntityDeletedResponse entityDeletedResponse = new EntityDeletedResponse();
                entityDeletedResponse.Id = id;
                entityDeletedResponse.Message = result ? $"Notificatioin configuration {id} was deleted succesfully." : $"Failed to delete notificatioin configuration {id}, check log for details.";

                return Ok(entityDeletedResponse);
            }
            catch (Exception ex)
            {
                ex.Data.Add("configuration id", id);
                _logger.LogError(new EventId(NotificationLoggingIds.DeleteItem, "Notification Service - Delete notification configuration"), ex, "An exception occurred while attempting to delete the notification configuration of a facility with an id of {id}", id);
                return StatusCode(500, ex);
            }

        }

        #endregion

    }
}
