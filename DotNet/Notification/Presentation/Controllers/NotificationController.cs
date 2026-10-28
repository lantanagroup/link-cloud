using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.Notification.Presentation.Controllers
{
    [Route("api/notification")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly ILogger<NotificationController> _logger;
        private readonly INotificationFactory _notificationFactory;    
        private int maxNotificationsPageSize = 20;        

        //Notification commands and queries
        private readonly IGetNotificationQuery _getNotificationQuery;
        private readonly IGetFacilityNotificatonsQuery _getFacilityNotificatonsQuery;
        private readonly IGetNotificationListQuery _getNotificationListQuery;

        public NotificationController(ILogger<NotificationController> logger, INotificationFactory notificationFactory, 
            IGetNotificationQuery getNotificationQuery, IGetFacilityNotificatonsQuery getFacilityNotificatonsQuery, 
            IGetNotificationListQuery getNotificationListQuery)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));

            _getNotificationQuery = getNotificationQuery ?? throw new ArgumentNullException(nameof(getNotificationQuery));
            _getFacilityNotificatonsQuery = getFacilityNotificatonsQuery ?? throw new ArgumentNullException(nameof(getFacilityNotificatonsQuery));
            _getNotificationListQuery = getNotificationListQuery ?? throw new ArgumentNullException(nameof(getNotificationListQuery));
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
        /// <param name="sortBy">Options: FacilityId, NotificationType, CreatedOn, LastModifiedOn, SentOn, defaults to CreatedOn</param>
        /// <param name="sortOrder">Ascending = 0, Descending = 1, defaults to Ascending</param>
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
        public async Task<ActionResult<PagedNotificationModel>> ListNotifications(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1)
        {
            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationsPageSize) { pageSize = maxNotificationsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }

                NotificationSearchRecord searchFilter = _notificationFactory.CreateNotificationSearchRecord(searchText, filterFacilityBy, filterNotificationTypeBy, sortBy, pageSize, pageNumber);
                _logger.LogNotificationListQuery(searchFilter);

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getNotificationListQuery.Execute(searchText, filterFacilityBy, filterNotificationTypeBy, createdOnStart, createdOnEnd, sentOnStart, sentOnEnd, sortBy, sortOrder, pageSize, pageNumber, HttpContext.RequestAborted);

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
        /// Returns a notification with the provided Id.
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
        public async Task<ActionResult<NotificationModel>> GetNotification(Guid id)
        {                 
            if (id == Guid.Empty) { return BadRequest("No notification id provided."); }           

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("notification-id", id);

            _logger.LogGetNotificationById(id.ToString());

            try
            {
                NotificationModel notification = await _getNotificationQuery.Execute(new NotificationId(id), HttpContext.RequestAborted);
                if (notification is null) { return NotFound(); }
                
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogGetNotificationByIdException(id.ToString(), ex.Message);
                throw;
            }

        }

        /// <summary>
        /// Returns a list of notifications based on the facility Id and provided filters.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="sortBy">Options: FacilityId, NotificationType, CreatedOn, LastModifiedOn, SentOn, defaults to CreatedOn</param>
        /// <param name="sortOrder">Ascending = 0, Descending = 1, defaults to Ascending</param>
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
        public async Task<ActionResult<PagedNotificationModel>> GetFacilityNotifications(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1)
        {          
            if (string.IsNullOrWhiteSpace(facilityId)) { return BadRequest("No facility id provided."); }

            //add id to current activity
            var activity = Activity.Current;
            activity?.AddTag("facility.id", facilityId);

            try
            {
                //make sure page size does not exceed the max page size allowed
                if (pageSize > maxNotificationsPageSize) { pageSize = maxNotificationsPageSize; }

                if (pageNumber < 1) { pageNumber = 1; }
                _logger.LogGetNotificationByFacilityId(facilityId);

                //Get list of audit events using supplied filters and pagination
                PagedNotificationModel notificationList = await _getFacilityNotificatonsQuery.Execute(facilityId, sortBy, sortOrder, pageSize, pageNumber, HttpContext.RequestAborted);

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
    }
}
