using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using Microsoft.AspNetCore.Mvc;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using System.Text.Json;
using LantanaGroup.Link.Notification.Presentation.Models;
using System.Diagnostics;
using System.Net;
using LantanaGroup.Link.Notification.Domain.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;

namespace LantanaGroup.Link.Notification.Presentation.Controllers
{
    [Route("api/notification/configuration")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class NotificationConfigurationController : ControllerBase
    {
        private readonly ILogger<NotificationConfigurationController> _logger;
        private readonly INotificationConfigurationFactory _configurationFactory;
        private readonly IFacilityClient _facilityClient;

        //Configuration commands and queries
        private readonly ICreateFacilityConfigurationCommand _createFacilityConfigurationCommand;
        private readonly IUpdateFacilityConfigurationCommand _updateFacilityConfigurationCommand;
        private readonly IFacilityConfigurationExistsQuery _facilityConfigurationExistsQuery;
        private readonly IGetFacilityConfigurationQuery _getFacilityConfigurationQuery;
        private readonly IGetNotificationConfigurationQuery _getNotificationConfigurationQuery;
        private readonly IGetFacilityConfigurationListQuery _getFacilityConfigurationListQuery;
        private readonly IDeleteFacilityConfigurationCommand _deleteFacilityConfigurationCommand;

        private readonly int maxNotificationConfigurationPageSize = 20;

        public NotificationConfigurationController(ILogger<NotificationConfigurationController> logger, INotificationConfigurationFactory configurationFactory, 
            IFacilityClient facilityClient, ICreateFacilityConfigurationCommand createFacilityConfigurationCommand, 
            IUpdateFacilityConfigurationCommand updateFacilityConfigurationCommand, IFacilityConfigurationExistsQuery facilityConfigurationExistsQuery, 
            IGetFacilityConfigurationQuery getFacilityConfigurationQuery, IGetNotificationConfigurationQuery getNotificationConfigurationQuery, 
            IGetFacilityConfigurationListQuery getFacilityConfigurationListQuery, IDeleteFacilityConfigurationCommand deleteFacilityConfigurationCommand)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationFactory = configurationFactory ?? throw new ArgumentNullException(nameof(configurationFactory));
            _facilityClient = facilityClient ?? throw new ArgumentNullException(nameof(facilityClient));
            
            _createFacilityConfigurationCommand = createFacilityConfigurationCommand ?? throw new ArgumentNullException(nameof(createFacilityConfigurationCommand));
            _updateFacilityConfigurationCommand = updateFacilityConfigurationCommand ?? throw new ArgumentNullException(nameof(updateFacilityConfigurationCommand));
            _facilityConfigurationExistsQuery = facilityConfigurationExistsQuery ?? throw new ArgumentNullException(nameof(facilityConfigurationExistsQuery));
            _getFacilityConfigurationQuery = getFacilityConfigurationQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationQuery));
            _getNotificationConfigurationQuery = getNotificationConfigurationQuery ?? throw new ArgumentNullException(nameof(getNotificationConfigurationQuery));
            _getFacilityConfigurationListQuery = getFacilityConfigurationListQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationListQuery));
            _deleteFacilityConfigurationCommand = deleteFacilityConfigurationCommand ?? throw new ArgumentNullException(nameof(deleteFacilityConfigurationCommand));
        }

        /// <summary>
        /// Returns a list of notification configurations based on the provided filters.
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="filterFacilityBy"></param>
        /// <param name="sortBy">Options: FacilityId, CreatedOn, LastModifiedOn</param>
        /// <param name="sortOrder">Ascending = 0, Descending = 1, defaults to Ascending</param>        
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>>
        /// <returns>
        ///     Success: 200     
        ///     Unautorized: 401
        ///     Forbidden: 403
        ///     Server Error: 500
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedNotificationConfigurationModel))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedNotificationConfigurationModel>> ListConfigurations(string? searchText, string? filterFacilityBy, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1)
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
                PagedNotificationConfigurationModel configList = await _getFacilityConfigurationListQuery.Execute(searchText, filterFacilityBy, sortBy, sortOrder, pageSize, pageNumber, HttpContext.RequestAborted);

                //add X-Pagination header for machine-readable pagination metadata
                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(configList.Metadata));

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
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityCreatedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityCreatedResponse>> CreateNotificationConfigurationAsync(NotificationConfigurationModel model)
        {
            //validate config values
            if (model is null) { return BadRequest("No notification configuration provided."); }

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
            NotificationConfigurationModel existingConfig = await _getFacilityConfigurationQuery.Execute(model.FacilityId, HttpContext.RequestAborted);
            if (existingConfig is not null)
            {
                var message = $"A configuration for facility {model.FacilityId} already exists.";
                _logger.LogInvalidNotificationConfigurationCreationWarning(model, message);
                return BadRequest(message);
            }

            try
            {
                //Create notification configuration
                CreateFacilityConfigurationModel createConfigModel = _configurationFactory.CreateFacilityConfigurationModelCreate(model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                var config = await _createFacilityConfigurationCommand.Execute(createConfigModel, HttpContext.RequestAborted);                

                return Ok(config);
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
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityCreatedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityCreatedResponse>> UpdateNotificationConfigurationAsync(NotificationConfigurationModel model)
        {

            //validate config values
            if (model is null) { return BadRequest("No notification configuration provided."); }

            if (string.IsNullOrEmpty(model.Id))
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
                bool exists = await _facilityConfigurationExistsQuery.Execute(NotificationConfigId.FromString(model.Id), HttpContext.RequestAborted);
                if (!exists)
                {
                    var notFoundMessage = $"No configuration with the id of {model.Id} was found.";
                    _logger.LogInvalidNotificationConfigurationUpdateWarning(model, notFoundMessage);
                    return BadRequest(notFoundMessage);
                }

                //Update notification configuration
                UpdateFacilityConfigurationModel config = _configurationFactory.UpdateFacilityConfigurationModelCreate(model.Id, model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                bool result = await _updateFacilityConfigurationCommand.Execute(config, HttpContext.RequestAborted);
                var msg = result ? "The notification configuration was updated succcessfully." : "Failed to update the notification configuration, check log for details.";
                EntityUpdateddResponse response = new EntityUpdateddResponse(msg, model.Id);

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
        [HttpGet("facility/{facilityId}")]
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
            activity?.AddTag("facility.id", facilityId);
            _logger.LogGetNotificationConfigurationByFacilityId(facilityId);

            try
            {
                NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(facilityId, HttpContext.RequestAborted);

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
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NotificationConfigurationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<NotificationConfigurationModel>> GetNotificationConfiguration(Guid id)
        {
            if (id == Guid.Empty)
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
                NotificationConfigurationModel config = await _getNotificationConfigurationQuery.Execute(new NotificationConfigId(id), HttpContext.RequestAborted);

                if (config == null) { return NotFound(); }
                _logger.LogGetNotificationConfigurationById(config.Id);

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationConfigurationByIdException(id.ToString(), ex.Message);
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
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EntityDeletedResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EntityDeletedResponse>> DeleteNotificationConfiguration(Guid id)
        {
            if (id == Guid.Empty)
            {
                var message = "No configuration id provided while attempting to delete a notification configuration.";
                _logger.LogNotificationConfigurationDeleteWarning(message);
                return BadRequest(message);
            }

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("notification.id", id);

            try
            {
                bool result = await _deleteFacilityConfigurationCommand.Execute(new NotificationConfigId(id), HttpContext.RequestAborted);

                EntityDeletedResponse entityDeletedResponse = new EntityDeletedResponse();
                entityDeletedResponse.Id = id.ToString();
                entityDeletedResponse.Message = result ? $"Notificatioin configuration {id} was deleted succesfully." : $"Failed to delete notificatioin configuration {id}, check log for details.";

                return Ok(entityDeletedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogNotificationConfigurationDeleteException(id.ToString(), ex.Message);
                throw;
            }

        }
    }
}
