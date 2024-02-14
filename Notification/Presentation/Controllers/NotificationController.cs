﻿using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Notification.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Diagnostics;
using System.Net;
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
        private readonly IFacilityClient _facilityClient;
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
            IGetNotificationListQuery getNotificationListQuery, IDeleteFacilityConfigurationCommand deleteFacilityConfigurationCommand, IFacilityClient facilityClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationFactory = configurationFactory ?? throw new ArgumentNullException(nameof(configurationFactory));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
            _facilityClient = facilityClient ?? throw new ArgumentNullException(nameof(facilityClient));

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
                if (pageSize > maxNotificationConfigurationPageSize) { pageSize = maxNotificationConfigurationPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                NotificationSearchRecord searchFilter = _notificationFactory.CreateNotificationSearchRecord(searchText, filterFacilityBy, filterNotificationTypeBy, sortBy, pageSize, pageNumber);
                _logger.LogNotificationListQuery(searchFilter);

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getNotificationListQuery.Execute(searchText, filterFacilityBy, filterNotificationTypeBy, createdOnStart, createdOnEnd, sentOnStart, sentOnEnd, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(notificationList.Metadata));

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
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(notificationList.Metadata));

                return Ok(notificationList);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationByFacilityIdException(facilityId, ex.Message);               
                throw;
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

                NotificationConfigurationSearchRecord searchRecord = _configurationFactory.CreateNotificationConfigurationSearchRecord(searchText, filterFacilityBy, sortBy, pageSize, pageNumber);
                _logger.LogNotificationConfigurationsListQuery(searchRecord);

                //Get list of audit events using supplied filters and pagination
                PagedNotificationConfigurationModel configList = await _getFacilityConfigurationListQuery.Execute(searchText, filterFacilityBy, sortBy, pageSize, pageNumber);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(configList.Metadata));

                return Ok(configList);

            }
            catch (Exception ex)
            {                
                NotificationConfigurationSearchRecord searchRecord = _configurationFactory.CreateNotificationConfigurationSearchRecord(searchText, filterFacilityBy, sortBy, pageSize, pageNumber);
                _logger.LogNotificationConfigurationsListQueryException(ex.Message, searchRecord);
                throw;
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
                var message = "The Facility Id was not provided in the new notification configuration.";
                _logger.LogInvalidNotificationConfigurationCreationWarning(model, message);                
                return BadRequest(message);
            }

            //Verify facility exists
            HttpResponseMessage verifyResponse = await _facilityClient.VerifyFacilityExists(model.FacilityId);
            if (!verifyResponse.IsSuccessStatusCode)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                switch (verifyResponse.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        throw new ArgumentException($"Facility with id {model.FacilityId} does not exist.");
                    case HttpStatusCode.InternalServerError:
                        throw new Exception($"Failed to verify facility with id {model.FacilityId}.");
                    default:
                        throw new Exception($"Failed to verify facility with id {model.FacilityId}. Status code: {verifyResponse.StatusCode}");
                }
            }

            //check if the facility already has an existing configuration
            NotificationConfigurationModel existingConfig = await _getFacilityConfigurationQuery.Execute(model.FacilityId);
            if (existingConfig is not null) 
            { 
                var message = $"A configuration for facility {model.FacilityId} already exists.";
                _logger.LogInvalidNotificationConfigurationCreationWarning(model, message);
                return BadRequest(message);        
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
                CreateFacilityConfigurationModel config = _configurationFactory.CreateFacilityConfigurationModelCreate(model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                _logger.LogNotificationConfigurationCreationException(config, ex.Message);
                throw;
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

            //validate config values
            if (model == null) { return BadRequest("No notification configuration provided."); }

            if(string.IsNullOrEmpty(model.Id)) 
            {
                var message = "The configuration Id was not provided in the updated notification configuration.";
                _logger.LogInvalidNotificationConfigurationUpdateWarning(model, message);
                return BadRequest(message);             
            }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                var message = "The Facility Id was not provided in the updated notification configuration.";
                _logger.LogInvalidNotificationConfigurationUpdateWarning(model, message);
                return BadRequest(message);
            }

            //Verify facility exists
            HttpResponseMessage verifyResponse = await _facilityClient.VerifyFacilityExists(model.FacilityId);
            if (!verifyResponse.IsSuccessStatusCode)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                switch (verifyResponse.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        throw new ArgumentException($"Facility with id {model.FacilityId} does not exist.");
                    case HttpStatusCode.InternalServerError:
                        throw new Exception($"Failed to verify facility with id {model.FacilityId}.");
                    default:
                        throw new Exception($"Failed to verify facility with id {model.FacilityId}. Status code: {verifyResponse.StatusCode}");
                }
            }            

            try
            {
                //check if the configuration exists
                bool exists = await _facilityConfigurationExistsQuery.Execute(model.Id);
                if (!exists) 
                {
                    var notFoundMessage = $"No configuration with the id of {model.Id} was found.";
                    _logger.LogInvalidNotificationConfigurationUpdateWarning(model, notFoundMessage);
                    return BadRequest(notFoundMessage); 
                }

                //Update notification configuration
                UpdateFacilityConfigurationModel config = _configurationFactory.UpdateFacilityConfigurationModelCreate(model.Id, model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                string id = await _updateFacilityConfigurationCommand.Execute(config);                
                EntityUpdateddResponse response = new EntityUpdateddResponse("The notification configuration was updated succcessfully.", id);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogNotificationConfigurationUpdateException(model, ex.Message);
                throw;
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
            if (string.IsNullOrEmpty(facilityId))
            {
                var message = "The Facility Id was not provided in the request for the notification configuration.";
                _logger.LogGetNotificationConfigurationByFacilityIdWarning(message);
                return BadRequest(message); 
            }

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("facility-id", facilityId);
            _logger.LogGetNotificationConfigurationByFacilityId(facilityId);

            try
            {
                NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(facilityId);

                if (config == null) { return NotFound(); }                

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationConfigurationByFacilityIdException(facilityId, ex.Message);
                throw;
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
            if (string.IsNullOrEmpty(id)) 
            {
                var message = "No configuration id provided while attempting to retrieve a notification configuration.";
                _logger.LogGetNotificationConfigurationByIdWarning(message);
                return BadRequest(message); 
            }

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("notification configuration id", id);

            try
            {
                NotificationConfigurationModel config = await _getNotificationConfigurationQuery.Execute(id);

                if (config == null) { return NotFound(); }
                _logger.LogGetNotificationConfigurationById(id);

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationConfigurationByIdException(id, ex.Message);
                throw;
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
                var message = "No configuration id provided while attempting to delete a notification configuration.";
                _logger.LogNotificationConfigurationDeleteWarning(message);
                return BadRequest(message);
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
                _logger.LogNotificationConfigurationDeleteException(id, ex.Message);
                throw;
            }

        }

        #endregion

    }
}
