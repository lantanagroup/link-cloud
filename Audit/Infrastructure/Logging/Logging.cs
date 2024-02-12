﻿using LantanaGroup.Link.Audit.Application.Commands;
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
        public static partial void LogAuditableEventConsumption(this ILogger logger, string facility, string serviceName, [LogProperties]CreateAuditEventModel auditEvent);
        
        [LoggerMessage(
            AuditLoggingIds.GenerateItems, 
            LogLevel.Information, 
            "New audit event created")]
        public static partial void LogAuditEventCreation(this ILogger logger, [LogProperties]AuditEntity auditEvent);

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
