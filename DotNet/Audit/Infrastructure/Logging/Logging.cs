using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Infrastructure.Logging
{
    public static partial class Logging
    {
        //Microsoft.Extensions.Telemetry
        //Microsoft.Extensions.Compliance.Redaction
        [LoggerMessage(
            AuditLoggingIds.EventConsumerInit,
            LogLevel.Information,
            "Started audit service consumer for topic(s) {topic} at {timestamp}.")]
        public static partial void LogConsumerStarted(this ILogger logger, string topic, DateTime timestamp);

        [LoggerMessage(
            AuditLoggingIds.EventConsumerObserved, 
            LogLevel.Information, 
            "New auditable event observed for facility {facility} from service {serviceName}.")]
        public static partial void LogAuditableEventConsumption(this ILogger logger, string facility, string serviceName, [LogProperties]AuditModel auditEvent);

        [LoggerMessage(
            AuditLoggingIds.DeadLetterException, 
            LogLevel.Error, 
            "Dead lettered Exception - {message}")]
        public static partial void LogDeadLetterException(this ILogger logger, string message);

        [LoggerMessage(
            AuditLoggingIds.TransientException, 
            LogLevel.Error, 
            "Transient Consumer Exception - {message}.")]
        public static partial void LogTransientException(this ILogger logger, string message);

        [LoggerMessage(
            AuditLoggingIds.InsertItem, 
            LogLevel.Error, 
            "Failed to create retry entity: {message}.")]
        public static partial void LogRetryEntityCreationException(this ILogger logger, string message);

        [LoggerMessage(
            AuditLoggingIds.GenerateItems, 
            LogLevel.Information, 
            "New audit event created")]
        public static partial void LogAuditEventCreation(this ILogger logger, [LogProperties]AuditLog auditEvent);

        [LoggerMessage(
            AuditLoggingIds.EventConsumerException, 
            LogLevel.Critical,
            "Consumer Exception for topic {topic}: {exceptionMessage}")]
        public static partial void LogConsumerException(this ILogger logger, string topic, string exceptionMessage);

        [LoggerMessage(
            AuditLoggingIds.EventConsumerOperationCanceled,
            LogLevel.Critical,
            "Consumer Operation Canceled for topic {topic} : {exceptionMessage}")]
        public static partial void LogOperationCanceledException(this ILogger logger, string topic, string exceptionMessage);

        [LoggerMessage(
            AuditLoggingIds.SearchPerformed,
            LogLevel.Information,
            "A search was generated for audit events.")]
        public static partial void LogAuditEventListQuery(this ILogger logger, [LogProperties]AuditSearchFilterRecord filter);
        
        [LoggerMessage(
            AuditLoggingIds.SearchException,
            LogLevel.Error,
            "An exception occurred while attempting to perform a search for audit events: {exceptionMessage}")]
        public static partial void LogAuditEventListQueryException(this ILogger logger, string exceptionMessage, [LogProperties]AuditSearchFilterRecord filter);

        [LoggerMessage(
            AuditLoggingIds.GetFacilityAuditEventsQuery,
            LogLevel.Information,
            "A request was made for a list of audit events for facility {facility}.")]
        public static partial void LogGetFacilityAuditEventsQuery(this ILogger logger, string facility);

        [LoggerMessage(
            AuditLoggingIds.GetFacilityAuditEventsQueryException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve a list of audit events for facility {facility}: {exceptionMessage}")]
        public static partial void LogGetFacilityAuditEventsQueryException(this ILogger logger, string facility, string exceptionMessage);

        [LoggerMessage(
            AuditLoggingIds.GetItem,
            LogLevel.Information,
            "A request was made for an audit event with an id of {id}.")]
        public static partial void LogGetAuditEventById(this ILogger logger, string id);

        [LoggerMessage(
            AuditLoggingIds.GetItemException,
            LogLevel.Error,
            "An exception occurred while attempting to retrieve an audit event with an id of {id}: {exceptionMessage}")]
        public static partial void LogGetAuditEventByIdException(this ILogger logger, string id, string exceptionMessage);
    }
}
