using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Notification.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
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
        private readonly INotificationFactory _notificationFactory;    
        private int maxNotificationsPageSize = 20;        

        //Notification commands and queries
        private readonly ICreateNotificationCommand _createNotificationCommand;
        private readonly ISendNotificationCommand _sendNotificationCommand;
        private readonly IValidateEmailAddressCommand _validateEmailAddressCommand;
        private readonly IGetNotificationQuery _getNotificationQuery;
        private readonly IGetFacilityNotificatonsQuery _getFacilityNotificatonsQuery;
        private readonly IGetNotificationListQuery _getNotificationListQuery;

        //Notification Configuration commands and queries
        private readonly IGetFacilityConfigurationQuery _getFacilityConfigurationQuery;


        public NotificationController(ILogger<NotificationController> logger, INotificationFactory notificationFactory, IGetFacilityConfigurationQuery getFacilityConfigurationQuery, 
            ICreateNotificationCommand createNotificationCommand, ISendNotificationCommand sendNotificationCommand, IValidateEmailAddressCommand validateEmailAddressCommand, 
            IGetNotificationQuery getNotificationQuery, IGetFacilityNotificatonsQuery getFacilityNotificatonsQuery, IGetNotificationListQuery getNotificationListQuery)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));

            _createNotificationCommand = createNotificationCommand ?? throw new ArgumentNullException(nameof(createNotificationCommand));
            _sendNotificationCommand = sendNotificationCommand ?? throw new ArgumentNullException(nameof(sendNotificationCommand));
            _validateEmailAddressCommand = validateEmailAddressCommand ?? throw new ArgumentNullException(nameof(validateEmailAddressCommand));
            _getNotificationQuery = getNotificationQuery ?? throw new ArgumentNullException(nameof(getNotificationQuery));
            _getFacilityNotificatonsQuery = getFacilityNotificatonsQuery ?? throw new ArgumentNullException(nameof(getFacilityNotificatonsQuery));
            _getNotificationListQuery = getNotificationListQuery ?? throw new ArgumentNullException(nameof(getNotificationListQuery));

            _getFacilityConfigurationQuery = getFacilityConfigurationQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationQuery));
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
                _logger.LogNotificationCreation(id, notification);
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
                CreateNotificationModel notification = _notificationFactory.CreateNotificationModelCreate(model.NotificationType, model.FacilityId, model.CorrelationId, model.Subject, model.Body, model.Recipients, model.Bcc);
                _logger.LogNotificationCreationException(notification, ex.Message);
                throw;
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
            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationsPageSize) { pageSize = maxNotificationsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                NotificationSearchRecord searchFilter = _notificationFactory.CreateNotificationSearchRecord(searchText, filterFacilityBy, filterNotificationTypeBy, sortBy, pageSize, pageNumber);
                _logger.LogNotificationListQuery(searchFilter);

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getNotificationListQuery.Execute(searchText, filterFacilityBy, filterNotificationTypeBy, createdOnStart, createdOnEnd, sentOnStart, sentOnEnd, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(notificationList.Metadata));

                return Ok(notificationList);

            }
            catch (Exception ex)
            {
                NotificationSearchRecord searchFilter = _notificationFactory.CreateNotificationSearchRecord(searchText, filterFacilityBy, filterNotificationTypeBy, sortBy, pageSize, pageNumber);
                _logger.LogNotificationListQueryException(ex.Message, searchFilter);
                throw;
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

                _logger.LogGetNotificationById(id);
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationByIdException(id, ex.Message);
                throw;
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
                _logger.LogGetNotificationByFacilityId(facilityId);

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getFacilityNotificatonsQuery.Execute(facilityId, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(notificationList.Metadata));

                return Ok(notificationList);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationByFacilityIdException(facilityId, ex.Message);               
                throw;
            }

        }

        #endregion

    }
}
