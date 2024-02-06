

using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Infrastructure.Logging
{

#pragma warning disable LOGGEN002 // Each logging method should use a unique event id
    public static partial class Logging
    {
        //Microsoft.Extensions.Telemetry
        //Microsoft.Extensions.Compliance.Redaction
        [LoggerMessage(
            NotificationLoggingIds.EventConsumerInit,
            LogLevel.Information, 
            "Started notification service consumer for topic(s) '{topic}' at {timestamp}.")]
        public static partial void LogConsumerStarted(this ILogger logger, string topic, DateTime timestamp);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerObserved, 
            LogLevel.Information,
            "New notification requested event observed for a '{notificationType}' notification.")]
        public static partial void LogNotificationRequestedConsumption(this ILogger logger, string notificationType);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerInvalidEmailAddress,
            LogLevel.Warning,
            "The email addresss '{emailAddress}' is invalid, removing from recipients.")]
        public static partial void LogNotificationRequestedInvalidEmailAddress(this ILogger logger, string emailAddress);


        [LoggerMessage(
            NotificationLoggingIds.GenerateItems, 
            LogLevel.Information, 
            "New notification created with id '{id}'.")]
        public static partial void LogNotificationCreation(this ILogger logger, string id, [LogProperties] CreateNotificationModel notification);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerException, 
            LogLevel.Critical,
            "Consumer Exception for topic '{topic}': {exceptionMessage}")]
        public static partial void LogConsumerException(this ILogger logger, string topic, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.EventConsumerOperationCanceled,
            LogLevel.Critical,
            "Consumer Operation Canceled for topic '{topic}' : {exceptionMessage}")]
        public static partial void LogOperationCanceledException(this ILogger logger, string topic, string exceptionMessage);

        [LoggerMessage(
            NotificationLoggingIds.SearchPerformed,
            LogLevel.Information,
            "A search was generated for notifications.")]
        public static partial void LogNotificationListQuery(this ILogger logger, [LogProperties] NotificationSearchRecord filter);
        
        [LoggerMessage(
            NotificationLoggingIds.SearchException,
            LogLevel.Error,
            "An exception occurred while attempting to perform a search for notification congurations: {exceptionMessage}")]
        public static partial void LogNotificationListQueryException(this ILogger logger, string exceptionMessage, [LogProperties] NotificationSearchRecord filter);

        [LoggerMessage(
            NotificationLoggingIds.SearchPerformed,
            LogLevel.Information,
            "A search was generated for notifications.")]

        public static partial void LogNotificationConfigurationsListQuery(this ILogger logger, [LogProperties] NotificationConfigurationSearchRecord filter);

        [LoggerMessage(
            NotificationLoggingIds.SearchException,
            LogLevel.Error,
            "An exception occurred while attempting to perform a search for notification configurations: {exceptionMessage}")]
        public static partial void LogNotificationConfigurationsListQueryException(this ILogger logger, string exceptionMessage, [LogProperties] NotificationConfigurationSearchRecord filter);

        [LoggerMessage(
            NotificationLoggingIds.GetItem,
            LogLevel.Information,
            "A request was made for an audit event with an id of {id}.")]
        public static partial void LogGetAuditEventById(this ILogger logger, string id);

        [LoggerMessage(
            NotificationLoggingIds.GetItemException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve an audit event with an id of '{id}': {exceptionMessage}")]
        public static partial void LogGetAuditEventByIdException(this ILogger logger, string id, string exceptionMessage);
    }
#pragma warning restore LOGGEN002 // Each logging method should use a unique event id
}
