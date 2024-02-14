
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Infrastructure.Logging
{
    public static partial class Logging
    {
        //Microsoft.Extensions.Telemetry
        //Microsoft.Extensions.Compliance.Redaction      
        [LoggerMessage(
            NotificationLoggingIds.EventConsumerInit,
            LogLevel.Information, 
            "Started notification service consumer for topic(s) {topic} at {timestamp}.")]
        public static partial void LogConsumerStarted(this ILogger logger, string topic, DateTime timestamp);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerObserved, 
            LogLevel.Information,
            "New notification requested event observed for a {notificationType} notification.")]
        public static partial void LogNotificationRequestedConsumption(this ILogger logger, string notificationType);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerInvalidEmailAddress,
            LogLevel.Warning,
            "The email addresss {emailAddress} is invalid, removing from recipients.")]
        public static partial void LogNotificationRequestedInvalidEmailAddress(this ILogger logger, string emailAddress);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerException,
            LogLevel.Critical,
            "Consumer Exception for topic {topic}: {exceptionMessage}")]
        public static partial void LogConsumerException(this ILogger logger, string topic, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerOperationCanceled,
            LogLevel.Critical,
            "Consumer Operation Canceled for topic {topic}: {exceptionMessage}")]
        public static partial void LogOperationCanceledException(this ILogger logger, string topic, string exceptionMessage);

        #region Notification Configuration Logging

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationCreation,
            LogLevel.Information,
            "New notification configuration created with id {id} for facility {facilityId}.")]
        public static partial void LogNotificationConfigurationCreation(this ILogger logger, string id, string facilityId, [LogProperties] CreateFacilityConfigurationModel config);

        [LoggerMessage(
            NotificationLoggingIds.InvalidNotificationConfigurationCreationRequestWarning,
            LogLevel.Warning,
            "A bad reqeust was made for the creation of a notification configuration: {exceptionMessage}")]
        public static partial void LogInvalidNotificationConfigurationCreationWarning(this ILogger logger, [LogProperties] NotificationConfigurationModel config, string exceptionMessage);
        
        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationCreationException,
            LogLevel.Error,
            "An exception occurred while attempting to create a new notification configuration: {exceptionMessage}")]
        public static partial void LogNotificationConfigurationCreationException(this ILogger logger, [LogProperties] CreateFacilityConfigurationModel config, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationUpdate,
            LogLevel.Information,
            "A notification configuration with an id of {id} was updated.")]
        public static partial void LogNotificationConfigurationUpdate(this ILogger logger, string id, [LogProperties] UpdateFacilityConfigurationModel config);

        [LoggerMessage(
            NotificationLoggingIds.InvalidNotificationConfigurationUpdateRequestWarning,
            LogLevel.Warning,
            "A bad reqeust was made for updating an existing notification configuration: {exceptionMessage}")]
        public static partial void LogInvalidNotificationConfigurationUpdateWarning(this ILogger logger, [LogProperties] NotificationConfigurationModel config, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationUpdateException,
            LogLevel.Error,
            "An exception occurred while attempting to update an existing notification configuration: {exceptionMessage}")]
        public static partial void LogNotificationConfigurationUpdateException(this ILogger logger, [LogProperties] NotificationConfigurationModel config, string exceptionMessage);


        [LoggerMessage(
            NotificationLoggingIds.GetNotificationConfigurationById,
            LogLevel.Information,
            "A request was made for a notification configuration with an id of {id}.")]
        public static partial void LogGetNotificationConfigurationById(this ILogger logger, string id);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationConfigurationByIdWarning,
            LogLevel.Warning,
            "A bad reqeust was made for a notification configuration by id: {exceptionMessage}")]
        public static partial void LogGetNotificationConfigurationByIdWarning(this ILogger logger, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationConfigurationByIdException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve the nofitication configuration with an id of {id}: {exceptionMessage}")]
        public static partial void LogGetNotificationConfigurationByIdException(this ILogger logger, string id, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationConfigurationByFacilityId,
            LogLevel.Information,
            "A request was made for a notification configuration with a facility id of {id}.")]
        public static partial void LogGetNotificationConfigurationByFacilityId(this ILogger logger, string id);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationConfigurationByFacilityIdWarning,
            LogLevel.Warning,
            "A bad reqeust was made for a notification configuration by facility: {exceptionMessage}")]
        public static partial void LogGetNotificationConfigurationByFacilityIdWarning(this ILogger logger, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationConfigurationByFacilityIdException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve the nofitication configuration with a facility id of {id}: {exceptionMessage}")]
        public static partial void LogGetNotificationConfigurationByFacilityIdException(this ILogger logger, string id, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationDelete,
            LogLevel.Information,
            "A request was made to delete a notification configuration with an id of {id}: {message}")]
        public static partial void LogNotificationConfigurationDeletion(this ILogger logger, string id, string message);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationDeleteWarning,
            LogLevel.Warning,
            "A bad reqeust was made for a notification configuration by facility: {exceptionMessage}")]
        public static partial void LogNotificationConfigurationDeleteWarning(this ILogger logger, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationDeleteException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve the nofitication configuration with a facility id of {id}.: {exceptionMessage}")]
        public static partial void LogNotificationConfigurationDeleteException(this ILogger logger, string id, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationListQuery,
            LogLevel.Information,
            "A search was generated for notifications.")]

        public static partial void LogNotificationConfigurationsListQuery(this ILogger logger, [LogProperties] NotificationConfigurationSearchRecord filter);

        [LoggerMessage(
            NotificationLoggingIds.NotificationConfigurationListQueryException,
            LogLevel.Error,
            "An exception occurred while attempting to perform a search for notification configurations: {exceptionMessage}")]
        public static partial void LogNotificationConfigurationsListQueryException(this ILogger logger, string exceptionMessage, [LogProperties] NotificationConfigurationSearchRecord filter);

        #endregion

        #region Notification Logging

        [LoggerMessage(
            NotificationLoggingIds.NotificationCreation,
            LogLevel.Information,
            "New notification created with id {id}.")]
        public static partial void LogNotificationCreation(this ILogger logger, string id, [LogProperties] CreateNotificationModel notification);

        [LoggerMessage(
            NotificationLoggingIds.NotificationCreationException,
            LogLevel.Error,
            "An exception occurred while attempting to create a new notification: {exceptionMessage}")]
        public static partial void LogNotificationCreationException(this ILogger logger, [LogProperties] CreateNotificationModel notification, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationById,
            LogLevel.Information,
            "A request was made for a notification with an id of {id}.")]
        public static partial void LogGetNotificationById(this ILogger logger, string id);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationByIdException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve the nofitication with an id of {id}: {exceptionMessage}")]
        public static partial void LogGetNotificationByIdException(this ILogger logger, string id, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationByFacilityId,
            LogLevel.Information,
            "A request was made for notifications with a facility id of {facilityId}.")]
        public static partial void LogGetNotificationByFacilityId(this ILogger logger, string facilityId);

        [LoggerMessage(
            NotificationLoggingIds.GetNotificationByFacilityIdException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve nofitications with a facility id of '{facilityId}': {exceptionMessage}")]
        public static partial void LogGetNotificationByFacilityIdException(this ILogger logger, string facilityId, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.NotificationListQuery,
            LogLevel.Information,
            "A search was generated for notifications.")]
        public static partial void LogNotificationListQuery(this ILogger logger, [LogProperties] NotificationSearchRecord filter);

        [LoggerMessage(
            NotificationLoggingIds.NotificationListQueryException,
            LogLevel.Error,
            "An exception occurred while attempting to perform a search for notification congurations: {exceptionMessage}")]
        public static partial void LogNotificationListQueryException(this ILogger logger, string exceptionMessage, [LogProperties] NotificationSearchRecord filter);

        #endregion

        #region Notification Channel Logging

        [LoggerMessage(
            NotificationLoggingIds.SendNotification,
            LogLevel.Information,
            "A notification was sent through channel {channel} at {timestamp}.")]
        public static partial void LogNotificationSent(this ILogger logger, string channel, DateTime timestamp);

        [LoggerMessage(
            NotificationLoggingIds.SendNotificationWarning,
            LogLevel.Warning,
            "An issue occured while proccessing a notification to be sent through channel {channel}: {exceptionMessage}")]
        public static partial void LogNotificationSentWarning(this ILogger logger, string channel, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.SendNotificationException,
            LogLevel.Error,
            "An exception occured while attempting to send a notification through channel {channel}: {exceptionMessage}")]
        public static partial void LogNotificationSentException(this ILogger logger, string channel, string exceptionMessage);

        #endregion

    }

}
